module Core

open Common
open CamlCompat

type evidence_instance = IR.evidence_instance
type inst_resolv_ctx = IR.inst_resolv_ctx

let arrow_class = HM.TNom "arrow_class"
let general_class = HM.TNom "general_class"
let namespace_class = HM.TNom "namespace"
let field_class = HM.TNom "field"
let module_class = HM.TNom "Module"
let type_type = HM.TNom "Type"

let int_ts =
  Map.ofList [
    for i in [8; 16; 32; 64] ->
    i, HM.TNom <| sprintf "i%d" i
  ]

let float_ts =
  Map.ofList [
    for i in [32; 64] ->
    i, HM.TNom <| sprintf "f%d" i
  ]

let str_t = HM.TNom "str"
let char_t = HM.TNom "char"
let bool_t = HM.TNom "bool"

let T x = HM.TApp(type_type, x)
let (|T|_|) x =
  match x with
  | HM.TApp(HM.TNom "Type", x) -> Some x
  | _ -> None

// to speed up instance resolution
// avoid applying it on TVar or TBound variants!
let rec get_type_head =
  function
  | HM.TNom _ as h -> h
  | HM.TApp(t, _)
  | HM.TForall(_, t)
  | HM.TArrow(HM.TImplicit _, t) ->
  get_type_head t
  | HM.TArrow(_, _) -> arrow_class
  | _ -> general_class

// type check global state
type global_st =
  { tcstate : HMUnification.tcstate
  // for global resolution
  ; global_implicits : (HM.t, evidence_instance darray) dict

  ; global_implicits_deltas : (HM.t, int) dict
  // for type holes and hints
  ; queries : (string * HM.t) darray
  // for gensym
  ; count : int ref
  // for gensym's uniqueness
  ; mutable current_module_name : string
  }

let empty_global_st () =
  { tcstate = HMUnification.mk_tcstate <| darray()
  ; global_implicits = dict()
  ; global_implicits_deltas = dict()
  ; queries = darray()
  ; count = ref 0
  ; current_module_name = "main"
  }


type local_st =
  { type_env : (symbol, HM.t) map
  // mapping user symbols to actual symbols
  ; symmap : (symbol, symbol) map
  // for local resolution
  ; local_implicits : inst_resolv_ctx
  // current position
  ; pos : pos
  }

let empty_local_st =
  { type_env = Map.empty
  ; symmap = Map.empty
  ; local_implicits = []
  ; pos = {line = 1; col = 1; filename = "<unknown>"}
  }


let alphabeta = [|for i = 'a' to 'z' do yield i|]

let gensym (g: global_st) (major : string) =
  let mutable i = !g.count + 1 in
  g.count := i + 1;
  let chars = darray() in
  while i <> 0 do
    let j = i % 26 in
    DArray.push chars <| alphabeta.[j]
    i <- i / 26
  done;
  // System.String.Concat : char iterable -> string
  sprintf "|%s|%s|%s|" major g.current_module_name (System.String.Concat chars)
