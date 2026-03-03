// YUSPEC v1.0 — Parser Implementation
// Entity-Behavior Programming (EBP)
#include "yuspec/v1_parser.h"
#include <sstream>
#include <stdexcept>
#include <algorithm>

namespace yuspec::v1 {

// ─── Helpers ─────────────────────────────────────────────────────────────
static ExprPtr make_expr(Expr::Data d, SrcPos p) {
  auto e = std::make_shared<Expr>(); e->data = std::move(d); e->pos = p; return e;
}
static ActionPtr make_action(Action::Data d, SrcPos p) {
  auto a = std::make_shared<Action>(); a->data = std::move(d); a->pos = p; return a;
}
static DeclPtr make_decl(Decl::Data d, SrcPos p) {
  auto decl = std::make_shared<Decl>(); decl->data = std::move(d); decl->pos = p; return decl;
}

// ─── Core ────────────────────────────────────────────────────────────────
Parser::Parser(Lexer& lex) : lex_(lex) { advance(); }

void Parser::advance()  { tok_ = lex_.next(); }
bool Parser::check(TK k) const { return tok_.kind == k; }
bool Parser::at_eof()   const { return tok_.kind == TK::END_OF_FILE; }

bool Parser::match(TK k) {
  if (check(k)) { advance(); return true; }
  return false;
}
Token Parser::expect(TK k, const char* what) {
  if (!check(k)) {
    std::ostringstream oss;
    oss << "Expected " << what << " but got '" << tok_.lexeme << "' (" << tk_name(tok_.kind) << ")";
    error(oss.str());
    // return dummy token for recovery
    return tok_;
  }
  Token t = tok_; advance(); return t;
}
void Parser::error(const std::string& msg) {
  diags_.push_back({ Diag::Error, tok_.pos, msg });
}
bool Parser::has_errors() const {
  return std::any_of(diags_.begin(), diags_.end(),
    [](const Diag& d){ return d.level == Diag::Error; });
}
void Parser::sync_to_decl() {
  while (!at_eof() && !check(TK::KW_DEFINE) && !check(TK::KW_IMPORT)) advance();
}

// ═══════════════════════════════════════════════════════════════════════════
// PROGRAM
// ═══════════════════════════════════════════════════════════════════════════
Program Parser::parse_program() {
  Program prog;
  while (!at_eof()) {
    try {
      prog.declarations.push_back(parse_decl());
    } catch (const std::exception& ex) {
      diags_.push_back({ Diag::Error, tok_.pos, ex.what() });
      sync_to_decl();
    }
  }
  return prog;
}

DeclPtr Parser::parse_decl() {
  if (check(TK::KW_IMPORT))        return parse_import();
  if (!check(TK::KW_DEFINE)) {
    error("Expected 'define' or 'import'");
    sync_to_decl();
    return nullptr;
  }
  advance(); // consume 'define'

  if (check(TK::KW_ENTITY))        return parse_entity();
  if (check(TK::KW_COMPONENT))     return parse_component();
  if (check(TK::KW_BEHAVIOR))      return parse_behavior();
  if (check(TK::KW_STATEMACHINE))  return parse_statemachine();
  if (check(TK::KW_EVENT))         return parse_event_decl();
  if (check(TK::KW_WORKFLOW))      return parse_workflow();
  if (check(TK::KW_ZONE))          return parse_zone();
  if (check(TK::KW_SYSTEM))        return parse_system();
  if (check(TK::KW_SCENARIO))      return parse_scenario();

  error("Unknown declaration kind after 'define'");
  sync_to_decl();
  return nullptr;
}

// ─── Import ──────────────────────────────────────────────────────────────
DeclPtr Parser::parse_import() {
  SrcPos p = tok_.pos;
  advance(); // 'import'
  Token path = expect(TK::LIT_STRING, "import path string");
  match(TK::SEMICOLON);
  ImportDecl d; d.path = path.lexeme; d.pos = p;
  return make_decl(std::move(d), p);
}

// ═══════════════════════════════════════════════════════════════════════════
// TYPE REFERENCE
// ═══════════════════════════════════════════════════════════════════════════
TypeRef Parser::parse_type_ref() {
  TypeRef tr; tr.pos = tok_.pos;
  if (check(TK::KW_LIST)) {
    tr.name = "list"; tr.is_list = true; advance();
    if (match(TK::LT)) {
      tr.params.push_back(parse_type_ref());
      expect(TK::GT, "'>' after list type param");
    }
    return tr;
  }
  if (check(TK::KW_MAP)) {
    tr.name = "map"; tr.is_map = true; advance();
    if (match(TK::LT)) {
      tr.params.push_back(parse_type_ref());
      expect(TK::COMMA, "',' between map key/value types");
      tr.params.push_back(parse_type_ref());
      expect(TK::GT, "'>' after map type params");
    }
    return tr;
  }
  static const TK builtin_types[] = {
    TK::KW_INT_TYPE, TK::KW_FLOAT_TYPE, TK::KW_BOOL_TYPE,
    TK::KW_STRING_TYPE, TK::KW_DURATION_TYPE, TK::KW_ANY, TK::KW_VOID
  };
  for (auto bt : builtin_types) {
    if (check(bt)) { tr.name = tok_.lexeme; advance(); return tr; }
  }
  if (check(TK::IDENT)) { tr.name = tok_.lexeme; advance(); return tr; }
  error("Expected type name");
  tr.name = "any";
  return tr;
}

// ─── Property ────────────────────────────────────────────────────────────
PropertyDecl Parser::parse_property() {
  SrcPos p = tok_.pos;
  expect(TK::KW_PROPERTY, "'property'");
  PropertyDecl pd; pd.pos = p;
  pd.name = expect(TK::IDENT, "property name").lexeme;
  pd.type = parse_type_ref();
  if (match(TK::KW_DEFAULT)) {
    pd.default_val = parse_expr();
  }
  match(TK::SEMICOLON);
  return pd;
}

// ═══════════════════════════════════════════════════════════════════════════
// ENTITY
// ═══════════════════════════════════════════════════════════════════════════
DeclPtr Parser::parse_entity() {
  SrcPos p = tok_.pos;
  advance(); // 'entity'
  EntityDecl d; d.pos = p;
  d.name = expect(TK::IDENT, "entity name").lexeme;
  expect(TK::LBRACE, "'{'");
  while (!check(TK::RBRACE) && !at_eof()) {
    if (check(TK::KW_PROPERTY)) {
      d.properties.push_back(parse_property());
    } else if (check(TK::KW_HAS)) {
      advance();
      // has BehaviorName or has component ComponentName
      if (check(TK::KW_COMPONENT)) { advance(); }
      d.has_behaviors.push_back(expect(TK::IDENT, "behavior/component name").lexeme);
      match(TK::SEMICOLON);
    } else {
      error("Expected 'property' or 'has' in entity body");
      advance();
    }
  }
  expect(TK::RBRACE, "'}'");
  return make_decl(std::move(d), p);
}

// ─── Component ───────────────────────────────────────────────────────────
DeclPtr Parser::parse_component() {
  SrcPos p = tok_.pos;
  advance(); // 'component'
  ComponentDecl d; d.pos = p;
  d.name = expect(TK::IDENT, "component name").lexeme;
  expect(TK::LBRACE, "'{'");
  while (!check(TK::RBRACE) && !at_eof()) {
    if (check(TK::KW_PROPERTY)) d.properties.push_back(parse_property());
    else { error("Expected 'property' in component body"); advance(); }
  }
  expect(TK::RBRACE, "'}'");
  return make_decl(std::move(d), p);
}

// ═══════════════════════════════════════════════════════════════════════════
// STATE
// ═══════════════════════════════════════════════════════════════════════════
StateDecl Parser::parse_state() {
  SrcPos p = tok_.pos;
  advance(); // 'state'
  StateDecl sd; sd.pos = p;
  sd.name = expect(TK::IDENT, "state name").lexeme;
  // options: initial, terminal, timeout <dur>, retry <int>
  while (!check(TK::SEMICOLON) && !check(TK::LBRACE) && !at_eof()) {
    if (match(TK::KW_INITIAL))   { sd.is_initial = true; continue; }
    if (match(TK::KW_TERMINAL))  { sd.is_terminal = true; continue; }
    if (check(TK::KW_TIMEOUT))   { advance(); sd.timeout_ms = parse_duration("state timeout"); continue; }
    if (check(TK::KW_RETRY))     { advance(); sd.retry_count = (int)expect(TK::LIT_INT,"retry count").int_val; continue; }
    break;
  }
  // Optional on_enter / on_exit blocks
  if (check(TK::LBRACE)) {
    advance();
    while (!check(TK::RBRACE) && !at_eof()) {
      sd.on_enter.push_back(parse_action());
    }
    expect(TK::RBRACE, "'}'");
  } else {
    match(TK::SEMICOLON);
  }
  return sd;
}

// ─── Duration helper ─────────────────────────────────────────────────────
double Parser::parse_duration(const char* ctx) {
  if (!check(TK::LIT_DURATION)) {
    error(std::string("Expected duration (e.g. 5s, 300ms, 2m) for ") + ctx);
    return 0.0;
  }
  double v = tok_.dur_ms; advance(); return v;
}

// ─── Trigger ─────────────────────────────────────────────────────────────
Trigger Parser::parse_trigger() {
  Trigger t; t.pos = tok_.pos;
  if (check(TK::KW_EVENT)) {
    advance(); t.kind = TriggerKind::Event;
    t.event_name = expect(TK::IDENT, "event name").lexeme;
  } else if (check(TK::KW_TIMEOUT)) {
    advance(); t.kind = TriggerKind::Timeout;
  } else if (check(TK::KW_WHEN)) {
    advance(); t.kind = TriggerKind::Condition;
    t.condition = parse_expr();
  } else {
    // bare identifier = event shorthand
    t.kind = TriggerKind::Event;
    t.event_name = expect(TK::IDENT, "event name or 'timeout' or 'when'").lexeme;
  }
  return t;
}

// ─── Transition ──────────────────────────────────────────────────────────
TransitionDecl Parser::parse_transition() {
  SrcPos p = tok_.pos;
  advance(); // 'on'
  TransitionDecl td; td.pos = p;
  td.from_trigger = parse_trigger();
  if (match(TK::KW_FROM)) {
    td.from_state = expect(TK::IDENT, "source state name").lexeme;
  }
  expect(TK::ARROW, "'->'");
  td.to_state = expect(TK::IDENT, "target state name").lexeme;
  if (check(TK::LBRACE)) {
    td.actions = parse_action_block();
  } else {
    match(TK::SEMICOLON);
  }
  return td;
}

// ─── Rule ────────────────────────────────────────────────────────────────
RuleDecl Parser::parse_rule() {
  SrcPos p = tok_.pos;
  advance(); // 'rule'
  RuleDecl rd; rd.pos = p;
  if (check(TK::IDENT)) { rd.name = tok_.lexeme; advance(); }
  expect(TK::KW_WHEN, "'when'");
  rd.condition = parse_expr();
  expect(TK::KW_THEN, "'then'");
  rd.actions = parse_action_block();
  return rd;
}

// ─── Handler ─────────────────────────────────────────────────────────────
HandlerDecl Parser::parse_handler() {
  SrcPos p = tok_.pos;
  advance(); // 'on'
  HandlerDecl hd; hd.pos = p;
  hd.trigger = parse_trigger();
  hd.actions = parse_action_block();
  return hd;
}

// ═══════════════════════════════════════════════════════════════════════════
// BEHAVIOR
// ═══════════════════════════════════════════════════════════════════════════
DeclPtr Parser::parse_behavior() {
  SrcPos p = tok_.pos;
  advance(); // 'behavior'
  BehaviorDecl d; d.pos = p;
  d.name = expect(TK::IDENT, "behavior name").lexeme;
  if (match(TK::KW_FOR)) {
    d.for_type = expect(TK::IDENT, "entity type").lexeme;
  }
  expect(TK::LBRACE, "'{'");
  while (!check(TK::RBRACE) && !at_eof()) {
    if (check(TK::KW_PROPERTY))    { d.properties.push_back(parse_property()); continue; }
    if (check(TK::KW_STATE))       { d.states.push_back(parse_state()); continue; }
    if (check(TK::KW_ON))          {
      // peek: is it a transition (has ->) or a handler?
      // We parse as handler; transition is 'on ... -> state'
      // We detect by parsing trigger then checking for arrow
      SrcPos sp = tok_.pos;
      advance(); // 'on'
      Trigger trig = parse_trigger();
      if (match(TK::KW_FROM) || check(TK::ARROW)) {
        // It's a transition
        TransitionDecl td; td.pos = sp; td.from_trigger = trig;
        if (check(TK::IDENT) && !check(TK::ARROW)) {
          td.from_state = tok_.lexeme; advance();
        }
        expect(TK::ARROW, "'->'");
        td.to_state = expect(TK::IDENT, "target state").lexeme;
        if (check(TK::LBRACE)) td.actions = parse_action_block();
        else match(TK::SEMICOLON);
        d.transitions.push_back(std::move(td));
      } else {
        // It's a handler
        HandlerDecl hd; hd.pos = sp; hd.trigger = trig;
        hd.actions = parse_action_block();
        d.handlers.push_back(std::move(hd));
      }
      continue;
    }
    if (check(TK::KW_RULE))        { d.rules.push_back(parse_rule()); continue; }
    error("Unexpected token in behavior body");
    advance();
  }
  expect(TK::RBRACE, "'}'");
  return make_decl(std::move(d), p);
}

// ─── StateMachine ─────────────────────────────────────────────────────────
DeclPtr Parser::parse_statemachine() {
  SrcPos p = tok_.pos;
  advance(); // 'statemachine'
  StateMachineDecl d; d.pos = p;
  d.name = expect(TK::IDENT, "statemachine name").lexeme;
  expect(TK::LBRACE, "'{'");
  while (!check(TK::RBRACE) && !at_eof()) {
    if (check(TK::KW_STATE)) { d.states.push_back(parse_state()); continue; }
    if (check(TK::KW_ON))    { advance(); // consume 'on'
      TransitionDecl td; td.pos = tok_.pos;
      td.from_trigger = parse_trigger();
      if (match(TK::KW_FROM)) td.from_state = expect(TK::IDENT,"from state").lexeme;
      expect(TK::ARROW,"'->'");
      td.to_state = expect(TK::IDENT,"to state").lexeme;
      if (check(TK::LBRACE)) td.actions = parse_action_block();
      else match(TK::SEMICOLON);
      d.transitions.push_back(std::move(td));
      continue;
    }
    error("Expected 'state' or 'on' in statemachine body"); advance();
  }
  expect(TK::RBRACE, "'}'");
  return make_decl(std::move(d), p);
}

// ─── Event ────────────────────────────────────────────────────────────────
DeclPtr Parser::parse_event_decl() {
  SrcPos p = tok_.pos;
  advance(); // 'event'
  EventDecl d; d.pos = p;
  d.name = expect(TK::IDENT, "event name").lexeme;
  if (check(TK::LBRACE)) {
    advance();
    while (!check(TK::RBRACE) && !at_eof()) {
      if (check(TK::KW_PROPERTY)) d.fields.push_back(parse_property());
      else { error("Expected 'property' in event body"); advance(); }
    }
    expect(TK::RBRACE, "'}'");
  } else {
    match(TK::SEMICOLON);
  }
  return make_decl(std::move(d), p);
}

// ─── Workflow ─────────────────────────────────────────────────────────────
DeclPtr Parser::parse_workflow() {
  SrcPos p = tok_.pos;
  advance(); // 'workflow'
  WorkflowDecl d; d.pos = p;
  d.name = expect(TK::IDENT, "workflow name").lexeme;
  expect(TK::LBRACE, "'{'");
  while (!check(TK::RBRACE) && !at_eof()) {
    if (check(TK::KW_PROPERTY)) { d.properties.push_back(parse_property()); continue; }
    if (check(TK::KW_STEP))     { d.steps.push_back(parse_step()); continue; }
    if (check(TK::KW_RULE))     { d.rules.push_back(parse_rule()); continue; }
    if (check(TK::KW_ON))       { advance(); HandlerDecl hd; hd.pos=tok_.pos; hd.trigger=parse_trigger(); hd.actions=parse_action_block(); d.handlers.push_back(std::move(hd)); continue; }
    error("Unexpected token in workflow body"); advance();
  }
  expect(TK::RBRACE, "'}'");
  return make_decl(std::move(d), p);
}

StepDecl Parser::parse_step() {
  SrcPos p = tok_.pos;
  advance(); // 'step'
  StepDecl sd; sd.pos = p;
  sd.name = expect(TK::IDENT, "step name").lexeme;
  while (!check(TK::SEMICOLON) && !check(TK::LBRACE) && !at_eof()) {
    if (check(TK::KW_ACTOR))   { advance(); sd.actor_type = expect(TK::IDENT,"actor type").lexeme; continue; }
    if (check(TK::KW_TIMEOUT)) { advance(); sd.timeout_ms = parse_duration("step timeout"); continue; }
    if (check(TK::KW_RETRY))   { advance(); sd.retry_count = (int)expect(TK::LIT_INT,"retry count").int_val; continue; }
    break;
  }
  if (check(TK::LBRACE)) {
    sd.on_enter = parse_action_block();
  } else {
    match(TK::SEMICOLON);
  }
  return sd;
}

// ─── Zone ─────────────────────────────────────────────────────────────────
DeclPtr Parser::parse_zone() {
  SrcPos p = tok_.pos;
  advance(); // 'zone'
  ZoneDecl d; d.pos = p;
  d.name = expect(TK::IDENT, "zone name").lexeme;
  expect(TK::LBRACE, "'{'");
  while (!check(TK::RBRACE) && !at_eof()) {
    if (check(TK::KW_PROPERTY)) { d.properties.push_back(parse_property()); continue; }
    if (check(TK::KW_SPAWN))    { d.spawns.push_back(parse_spawn_action()); continue; }
    if (check(TK::KW_RULE))     { d.rules.push_back(parse_rule()); continue; }
    // terrain NxM
    if (check(TK::IDENT) && tok_.lexeme == "terrain") {
      advance();
      d.terrain_w = (int)expect(TK::LIT_INT,"terrain width").int_val;
      // 'x' can appear as ident or as X_DIM
      if (check(TK::IDENT) && tok_.lexeme == "x") advance();
      else if (check(TK::X_DIM)) advance();
      else expect(TK::X_DIM, "'x' in WxH");
      d.terrain_h = (int)expect(TK::LIT_INT,"terrain height").int_val;
      d.has_terrain = true;
      match(TK::SEMICOLON);
      continue;
    }
    error("Unexpected token in zone body"); advance();
  }
  expect(TK::RBRACE, "'}'");
  return make_decl(std::move(d), p);
}

// ─── System ──────────────────────────────────────────────────────────────
DeclPtr Parser::parse_system() {
  SrcPos p = tok_.pos;
  advance(); // 'system'
  SystemDecl d; d.pos = p;
  d.name = expect(TK::IDENT, "system name").lexeme;
  if (match(TK::KW_TIMEOUT)) { // reuse 'tick' via timeout for now; lexer doesn't have 'tick'
    // "tick <duration>"
    d.tick_ms = parse_duration("system tick");
  }
  // optional 'tick <duration>'
  if (check(TK::IDENT) && tok_.lexeme == "tick") {
    advance(); d.tick_ms = parse_duration("system tick");
  }
  expect(TK::LBRACE, "'{'");
  while (!check(TK::RBRACE) && !at_eof()) {
    if (check(TK::KW_ON))   { advance(); HandlerDecl hd; hd.pos=tok_.pos; hd.trigger=parse_trigger(); hd.actions=parse_action_block(); d.handlers.push_back(std::move(hd)); continue; }
    if (check(TK::KW_RULE)) { d.rules.push_back(parse_rule()); continue; }
    error("Unexpected token in system body"); advance();
  }
  expect(TK::RBRACE, "'}'");
  return make_decl(std::move(d), p);
}

// ─── Scenario ────────────────────────────────────────────────────────────
DeclPtr Parser::parse_scenario() {
  SrcPos p = tok_.pos;
  advance(); // 'scenario'
  ScenarioDecl d; d.pos = p;
  d.name = expect(TK::IDENT, "scenario name").lexeme;
  d.steps = parse_action_block();
  return make_decl(std::move(d), p);
}

// ═══════════════════════════════════════════════════════════════════════════
// SPAWN ACTION
// ═══════════════════════════════════════════════════════════════════════════
SpawnAction Parser::parse_spawn_action() {
  advance(); // 'spawn'
  SpawnAction sa;
  sa.entity_type = parse_type_ref();
  // Options: count, at, placement, with
  while (!check(TK::SEMICOLON) && !check(TK::RBRACE) && !at_eof()) {
    if (check(TK::IDENT) && tok_.lexeme == "count") { advance(); sa.count = parse_expr(); continue; }
    if (check(TK::IDENT) && tok_.lexeme == "at")    { advance(); sa.at_expr = parse_expr(); continue; }
    if (check(TK::IDENT) && tok_.lexeme == "placement") { advance(); sa.placement = parse_expr(); continue; }
    if (check(TK::KW_IN)) { advance(); sa.placement = parse_expr(); continue; } // 'in terrain random'
    if (check(TK::IDENT) && tok_.lexeme == "with") {
      advance();
      std::string key = expect(TK::IDENT,"property name").lexeme;
      expect(TK::ASSIGN,"'='");
      sa.with_props.push_back({key, parse_expr()});
      while (match(TK::COMMA)) {
        key = expect(TK::IDENT,"property name").lexeme;
        expect(TK::ASSIGN,"'='");
        sa.with_props.push_back({key, parse_expr()});
      }
      continue;
    }
    break;
  }
  match(TK::SEMICOLON);
  return sa;
}

// ═══════════════════════════════════════════════════════════════════════════
// ACTIONS
// ═══════════════════════════════════════════════════════════════════════════
std::vector<ActionPtr> Parser::parse_action_block() {
  std::vector<ActionPtr> actions;
  if (check(TK::LBRACE)) {
    advance();
    while (!check(TK::RBRACE) && !at_eof()) {
      actions.push_back(parse_action());
    }
    expect(TK::RBRACE, "'}'");
  } else {
    actions.push_back(parse_action());
  }
  return actions;
}

ActionPtr Parser::parse_action() {
  SrcPos p = tok_.pos;

  if (check(TK::KW_IF))      return parse_if_action();
  if (check(TK::KW_WHILE))   return parse_while_action();
  if (check(TK::KW_FOR_EACH)) return parse_foreach_action();

  if (check(TK::KW_SPAWN)) {
    SpawnAction sa = parse_spawn_action();
    return make_action(std::move(sa), p);
  }

  if (check(TK::KW_EMIT)) {
    advance();
    EmitAction ea; ea.event_name = expect(TK::IDENT,"event name").lexeme;
    if (match(TK::LBRACE)) {
      while (!check(TK::RBRACE) && !at_eof()) {
        std::string k = expect(TK::IDENT,"field name").lexeme;
        expect(TK::COLON,"':'");
        ea.fields.push_back({k, parse_expr()});
        match(TK::COMMA);
      }
      expect(TK::RBRACE,"'}'");
    }
    if (check(TK::IDENT) && tok_.lexeme == "to") { advance(); ea.target = parse_expr(); }
    match(TK::SEMICOLON);
    return make_action(std::move(ea), p);
  }

  if (check(TK::KW_SET)) {
    advance();
    SetAction sa; sa.lhs = parse_expr();
    if      (match(TK::ASSIGN))        sa.op = "=";
    else if (match(TK::PLUS_ASSIGN))   sa.op = "+=";
    else if (match(TK::MINUS_ASSIGN))  sa.op = "-=";
    else { expect(TK::ASSIGN,"'='"); sa.op = "="; }
    sa.rhs = parse_expr();
    match(TK::SEMICOLON);
    return make_action(std::move(sa), p);
  }

  if (check(TK::KW_CALL)) {
    advance();
    CallAction ca; ca.callee = parse_expr();
    if (match(TK::LPAREN)) {
      while (!check(TK::RPAREN) && !at_eof()) {
        ca.args.push_back(parse_expr());
        match(TK::COMMA);
      }
      expect(TK::RPAREN,"')'");
    }
    match(TK::SEMICOLON);
    return make_action(std::move(ca), p);
  }

  if (check(TK::KW_DESTROY)) {
    advance(); DestroyAction da; da.target = parse_expr();
    match(TK::SEMICOLON);
    return make_action(std::move(da), p);
  }

  if (check(TK::KW_ATTACH)) {
    advance(); AttachAction aa;
    aa.behavior = expect(TK::IDENT,"behavior name").lexeme;
    if (check(TK::IDENT) && tok_.lexeme == "to") { advance(); aa.target = parse_expr(); }
    else aa.target = make_expr(IdentExpr{"self"}, p);
    match(TK::SEMICOLON);
    return make_action(std::move(aa), p);
  }

  if (check(TK::KW_DETACH)) {
    advance(); DetachAction da;
    da.behavior = expect(TK::IDENT,"behavior name").lexeme;
    if (check(TK::IDENT) && tok_.lexeme == "to") { advance(); da.target = parse_expr(); }
    else da.target = make_expr(IdentExpr{"self"}, p);
    match(TK::SEMICOLON);
    return make_action(std::move(da), p);
  }

  if (check(TK::KW_LOG)) {
    advance(); LogAction la; la.message = parse_expr();
    match(TK::SEMICOLON);
    return make_action(std::move(la), p);
  }

  if (check(TK::KW_ASSERT)) {
    advance(); AssertAction aa; aa.condition = parse_expr();
    if (check(TK::LIT_STRING)) { aa.message = tok_.lexeme; advance(); }
    match(TK::SEMICOLON);
    return make_action(std::move(aa), p);
  }

  if (check(TK::KW_WAIT)) {
    advance(); WaitAction wa;
    wa.dur_ms = parse_duration("wait");
    match(TK::SEMICOLON);
    return make_action(std::move(wa), p);
  }

  if (check(TK::KW_TRANSITION)) {
    advance(); TransitionAction ta;
    ta.target_state = expect(TK::IDENT,"state name").lexeme;
    match(TK::SEMICOLON);
    return make_action(std::move(ta), p);
  }

  if (check(TK::KW_RETRY)) {
    advance(); match(TK::SEMICOLON);
    return make_action(RetryAction{}, p);
  }

  if (check(TK::KW_FAIL)) {
    advance(); FailAction fa;
    if (!check(TK::SEMICOLON)) fa.message = parse_expr();
    match(TK::SEMICOLON);
    return make_action(std::move(fa), p);
  }

  if (check(TK::KW_BREAK)) {
    advance(); match(TK::SEMICOLON);
    return make_action(BreakAction{}, p);
  }
  if (check(TK::KW_CONTINUE)) {
    advance(); match(TK::SEMICOLON);
    return make_action(ContinueAction{}, p);
  }
  if (check(TK::KW_RETURN)) {
    advance(); ReturnAction ra;
    if (!check(TK::SEMICOLON)) ra.value = parse_expr();
    match(TK::SEMICOLON);
    return make_action(std::move(ra), p);
  }

  if (check(TK::KW_EXPECT)) {
    advance(); ExpectAction ea; ea.condition = parse_expr();
    if (check(TK::LIT_STRING)) { advance(); } // consume optional message string
    if (match(TK::KW_WITHIN)) { ea.within_ms = parse_duration("expect within"); }
    match(TK::SEMICOLON);
    return make_action(std::move(ea), p);
  }

  // let <name> = <expr>
  if (check(TK::KW_LET)) {
    advance(); LetAction la;
    la.name = expect(TK::IDENT, "variable name").lexeme;
    expect(TK::ASSIGN, "'='");
    la.init = parse_expr();
    match(TK::SEMICOLON);
    return make_action(std::move(la), p);
  }

  // Fallback: treat as expression statement (call)
  error("Unknown action '" + tok_.lexeme + "'");
  advance(); match(TK::SEMICOLON);
  return make_action(RetryAction{}, p);
}

ActionPtr Parser::parse_if_action() {
  SrcPos p = tok_.pos; advance(); // 'if'
  IfAction ia;
  expect(TK::LPAREN,"'('"); ia.condition = parse_expr(); expect(TK::RPAREN,"')'");
  ia.then_body = parse_action_block();
  if (match(TK::KW_ELSE)) ia.else_body = parse_action_block();
  return make_action(std::move(ia), p);
}

ActionPtr Parser::parse_while_action() {
  SrcPos p = tok_.pos; advance(); // 'while'
  WhileAction wa;
  expect(TK::LPAREN,"'('"); wa.condition = parse_expr(); expect(TK::RPAREN,"')'");
  wa.body = parse_action_block();
  return make_action(std::move(wa), p);
}

ActionPtr Parser::parse_foreach_action() {
  SrcPos p = tok_.pos; advance(); // 'foreach'
  ForeachAction fa;
  bool has_parens = match(TK::LPAREN);
  fa.var = expect(TK::IDENT,"loop variable").lexeme;
  expect(TK::KW_IN,"'in'");
  fa.collection = parse_expr();
  if (has_parens) expect(TK::RPAREN,"')'");
  fa.body = parse_action_block();
  return make_action(std::move(fa), p);
}

// ═══════════════════════════════════════════════════════════════════════════
// EXPRESSIONS — Pratt/precedence climbing
// ═══════════════════════════════════════════════════════════════════════════
ExprPtr Parser::parse_expr() { return parse_or(); }

ExprPtr Parser::parse_or() {
  auto left = parse_and();
  while (check(TK::OR_OR)) {
    std::string op = tok_.lexeme; SrcPos p = tok_.pos; advance();
    auto right = parse_and();
    left = make_expr(BinaryExpr{op, left, right}, p);
  }
  return left;
}
ExprPtr Parser::parse_and() {
  auto left = parse_equality();
  while (check(TK::AND_AND)) {
    std::string op = tok_.lexeme; SrcPos p = tok_.pos; advance();
    auto right = parse_equality();
    left = make_expr(BinaryExpr{op, left, right}, p);
  }
  return left;
}
ExprPtr Parser::parse_equality() {
  auto left = parse_comparison();
  while (check(TK::EQ) || check(TK::NEQ)) {
    std::string op = tok_.lexeme; SrcPos p = tok_.pos; advance();
    auto right = parse_comparison();
    left = make_expr(BinaryExpr{op, left, right}, p);
  }
  return left;
}
ExprPtr Parser::parse_comparison() {
  auto left = parse_addition();
  while (check(TK::LT)||check(TK::GT)||check(TK::LTE)||check(TK::GTE)) {
    std::string op = tok_.lexeme; SrcPos p = tok_.pos; advance();
    auto right = parse_addition();
    left = make_expr(BinaryExpr{op, left, right}, p);
  }
  return left;
}
ExprPtr Parser::parse_addition() {
  auto left = parse_multiplication();
  while (check(TK::PLUS)||check(TK::MINUS)) {
    std::string op = tok_.lexeme; SrcPos p = tok_.pos; advance();
    auto right = parse_multiplication();
    left = make_expr(BinaryExpr{op, left, right}, p);
  }
  return left;
}
ExprPtr Parser::parse_multiplication() {
  auto left = parse_unary();
  while (check(TK::STAR)||check(TK::SLASH)||check(TK::PERCENT)) {
    std::string op = tok_.lexeme; SrcPos p = tok_.pos; advance();
    auto right = parse_unary();
    left = make_expr(BinaryExpr{op, left, right}, p);
  }
  return left;
}
ExprPtr Parser::parse_unary() {
  if (check(TK::BANG)||check(TK::MINUS)) {
    std::string op = tok_.lexeme; SrcPos p = tok_.pos; advance();
    return make_expr(UnaryExpr{op, parse_unary()}, p);
  }
  return parse_postfix();
}
ExprPtr Parser::parse_postfix() {
  auto expr = parse_primary();
  while (true) {
    SrcPos p = tok_.pos;
    if (match(TK::DOT)) {
      std::string field = expect(TK::IDENT,"field name").lexeme;
      expr = make_expr(MemberExpr{expr, field}, p);
    } else if (match(TK::LBRACKET)) {
      auto idx = parse_expr();
      expect(TK::RBRACKET,"']'");
      expr = make_expr(IndexExpr{expr, idx}, p);
    } else if (check(TK::LPAREN)) {
      advance();
      CallExpr ce; ce.callee = expr;
      while (!check(TK::RPAREN) && !at_eof()) {
        ce.args.push_back(parse_expr());
        match(TK::COMMA);
      }
      expect(TK::RPAREN,"')'");
      expr = make_expr(std::move(ce), p);
    } else {
      break;
    }
  }
  return expr;
}

ExprPtr Parser::parse_primary() {
  SrcPos p = tok_.pos;

  if (check(TK::LIT_INT)) {
    LiteralExpr l; l.kind = LiteralExpr::Kind::Int; l.int_val = tok_.int_val;
    advance(); return make_expr(std::move(l), p);
  }
  if (check(TK::LIT_FLOAT)) {
    LiteralExpr l; l.kind = LiteralExpr::Kind::Float; l.flt_val = tok_.flt_val;
    advance(); return make_expr(std::move(l), p);
  }
  if (check(TK::LIT_STRING)) {
    LiteralExpr l; l.kind = LiteralExpr::Kind::String; l.str_val = tok_.lexeme;
    advance(); return make_expr(std::move(l), p);
  }
  if (check(TK::LIT_BOOL_TRUE)) {
    LiteralExpr l; l.kind = LiteralExpr::Kind::Bool; l.bool_val = true;
    advance(); return make_expr(std::move(l), p);
  }
  if (check(TK::LIT_BOOL_FALSE)) {
    LiteralExpr l; l.kind = LiteralExpr::Kind::Bool; l.bool_val = false;
    advance(); return make_expr(std::move(l), p);
  }
  if (check(TK::LIT_DURATION)) {
    LiteralExpr l; l.kind = LiteralExpr::Kind::Duration; l.dur_ms = tok_.dur_ms;
    advance(); return make_expr(std::move(l), p);
  }
  if (check(TK::LIT_NULL)) {
    LiteralExpr l; l.kind = LiteralExpr::Kind::Null;
    advance(); return make_expr(std::move(l), p);
  }
  if (check(TK::IDENT)) {
    IdentExpr ie; ie.name = tok_.lexeme; advance();
    return make_expr(std::move(ie), p);
  }
  // Keyword-as-ident (self, this)
  if (check(TK::KW_FOR) && tok_.lexeme == "self") { // shouldn't happen but guard
    IdentExpr ie; ie.name = "self"; advance(); return make_expr(std::move(ie), p);
  }
  if (check(TK::LPAREN)) {
    advance(); auto e = parse_expr(); expect(TK::RPAREN,"')'"); return e;
  }
  // List literal
  if (check(TK::LBRACKET)) {
    advance(); ListExpr le;
    while (!check(TK::RBRACKET) && !at_eof()) {
      le.elements.push_back(parse_expr()); match(TK::COMMA);
    }
    expect(TK::RBRACKET,"']'");
    return make_expr(std::move(le), p);
  }
  // Map literal  { key: val, ... }
  if (check(TK::LBRACE)) {
    advance(); MapExpr me;
    while (!check(TK::RBRACE) && !at_eof()) {
      auto k = parse_expr(); expect(TK::COLON,"':'"); auto v = parse_expr();
      me.entries.push_back({k, v}); match(TK::COMMA);
    }
    expect(TK::RBRACE,"'}'");
    return make_expr(std::move(me), p);
  }

  // Treat known keywords as identifiers when they appear in expression position
  // (e.g. "random", "terrain", entity type names used as-is in zones)
  if (tok_.kind != TK::END_OF_FILE && tok_.kind != TK::RBRACE &&
      tok_.kind != TK::SEMICOLON) {
    IdentExpr ie; ie.name = tok_.lexeme; advance();
    return make_expr(std::move(ie), p);
  }

  error("Expected expression");
  LiteralExpr l; l.kind = LiteralExpr::Kind::Null;
  return make_expr(std::move(l), p);
}

} // namespace yuspec::v1
