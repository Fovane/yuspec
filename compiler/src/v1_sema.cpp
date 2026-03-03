// YUSPEC v1.0 — Semantic Analyzer
#include "yuspec/v1_sema.h"
#include <sstream>
#include <algorithm>

namespace yuspec::v1 {

void Sema::error(SrcPos p, const std::string& msg) {
  diags_.push_back({ Diag::Error, p, msg });
}
void Sema::warn(SrcPos p, const std::string& msg) {
  diags_.push_back({ Diag::Warning, p, msg });
}

SemaResult Sema::analyze(const Program& prog) {
  global_ = std::make_shared<Scope>();
  // Built-in symbols
  global_->define("self",  Type::make_any(), {});
  global_->define("true",  Type::make_bool(), {});
  global_->define("false", Type::make_bool(), {});
  global_->define("null",  Type::make_null(), {});

  collect_declarations(prog);

  for (auto& d : prog.declarations) {
    if (d) check_decl(d);
  }

  SemaResult res;
  res.diagnostics = diags_;
  res.ok = std::none_of(diags_.begin(), diags_.end(),
    [](const Diag& d){ return d.level == Diag::Error; });
  return res;
}

// ─── First pass: register all top-level names ────────────────────────────
void Sema::collect_declarations(const Program& prog) {
  for (auto& d : prog.declarations) {
    if (!d) continue;
    if (d->is<EntityDecl>()) {
      auto& e = d->as<EntityDecl>();
      if (entities_.count(e.name)) error(e.pos, "Duplicate entity: " + e.name);
      entities_[e.name] = &e;
      global_->define(e.name, Type::make_named(TypeKind::Entity, e.name), e.pos);
    } else if (d->is<BehaviorDecl>()) {
      auto& b = d->as<BehaviorDecl>();
      if (behaviors_.count(b.name)) error(b.pos, "Duplicate behavior: " + b.name);
      behaviors_[b.name] = &b;
    } else if (d->is<EventDecl>()) {
      auto& ev = d->as<EventDecl>();
      if (events_.count(ev.name)) error(ev.pos, "Duplicate event: " + ev.name);
      events_[ev.name] = &ev;
    } else if (d->is<ComponentDecl>()) {
      auto& c = d->as<ComponentDecl>();
      if (components_.count(c.name)) error(c.pos, "Duplicate component: " + c.name);
      components_[c.name] = &c;
    }
  }
}

void Sema::check_decl(const DeclPtr& d) {
  if (d->is<EntityDecl>())    check_entity(d->as<EntityDecl>());
  else if (d->is<BehaviorDecl>())  check_behavior(d->as<BehaviorDecl>());
  else if (d->is<WorkflowDecl>())  check_workflow(d->as<WorkflowDecl>());
  else if (d->is<ZoneDecl>())      check_zone(d->as<ZoneDecl>());
  else if (d->is<ScenarioDecl>())  check_scenario(d->as<ScenarioDecl>());
  // StateMachineDecl, EventDecl, ComponentDecl, ImportDecl — structurally valid if parsed
}

void Sema::check_entity(const EntityDecl& d) {
  // Check that referenced behaviors exist
  for (auto& bname : d.has_behaviors) {
    if (!behaviors_.count(bname) && !components_.count(bname)) {
      warn(d.pos, "Entity '" + d.name + "' references unknown behavior/component: " + bname);
    }
  }
}

void Sema::check_behavior(const BehaviorDecl& d) {
  // Check for_type
  if (!d.for_type.empty() && !entities_.count(d.for_type)) {
    warn(d.pos, "Behavior '" + d.name + "' targets unknown entity type: " + d.for_type);
  }
  // Check initial state exists
  auto& states = d.states;
  int initial_count = 0;
  for (auto& s : states) if (s.is_initial) initial_count++;
  if (!states.empty() && initial_count == 0) {
    warn(d.pos, "Behavior '" + d.name + "' has states but no 'initial' state");
  }
  if (initial_count > 1) {
    error(d.pos, "Behavior '" + d.name + "' has multiple 'initial' states");
  }
  // Check transitions reference valid states
  for (auto& td : d.transitions) {
    check_transition(td, states);
  }
  // Check actions in handlers and rules
  Scope scope; scope.parent = global_;
  for (auto& pd : d.properties) {
    scope.define(pd.name, Type::from_ref(pd.type), pd.pos);
  }
  for (auto& h : d.handlers) check_actions(h.actions, scope);
  for (auto& r : d.rules)    check_actions(r.actions, scope);
  for (auto& t : d.transitions) check_actions(t.actions, scope);
  for (auto& s : states) {
    check_actions(s.on_enter, scope);
    check_actions(s.on_exit, scope);
  }
}

void Sema::check_transition(const TransitionDecl& td, const std::vector<StateDecl>& states) {
  auto state_exists = [&](const std::string& name) {
    return std::any_of(states.begin(), states.end(),
      [&](const StateDecl& s){ return s.name == name; });
  };
  if (td.from_state && !state_exists(*td.from_state)) {
    error(td.pos, "Transition from unknown state: " + *td.from_state);
  }
  if (!state_exists(td.to_state)) {
    error(td.pos, "Transition to unknown state: " + td.to_state);
  }
  // Event trigger references known event?
  if (td.from_trigger.kind == TriggerKind::Event) {
    if (!events_.count(td.from_trigger.event_name)) {
      // not an error — events can be external
      // warn if in strict mode
    }
  }
}

void Sema::check_workflow(const WorkflowDecl& d) {
  Scope scope; scope.parent = global_;
  for (auto& pd : d.properties) scope.define(pd.name, Type::from_ref(pd.type), pd.pos);
  // Check steps reference each other in rules correctly — basic pass
  for (auto& r : d.rules) check_actions(r.actions, scope);
  for (auto& h : d.handlers) check_actions(h.actions, scope);
}

void Sema::check_zone(const ZoneDecl& d) {
  // Check spawn entity types exist
  for (auto& sa : d.spawns) {
    if (!entities_.count(sa.entity_type.name) && sa.entity_type.name != "any") {
      warn(d.pos, "Zone '" + d.name + "' spawns unknown entity type: " + sa.entity_type.name);
    }
  }
  Scope scope; scope.parent = global_;
  for (auto& r : d.rules) check_actions(r.actions, scope);
}

void Sema::check_scenario(const ScenarioDecl& d) {
  Scope scope; scope.parent = global_;
  check_actions(d.steps, scope);
}

void Sema::check_actions(const std::vector<ActionPtr>& actions, Scope& scope) {
  for (auto& a : actions) if (a) check_action(*a, scope);
}

void Sema::check_action(const Action& a, Scope& scope) {
  if (a.is<SetAction>()) {
    auto& sa = a.as<SetAction>();
    check_expr(*sa.lhs, scope);
    check_expr(*sa.rhs, scope);
  } else if (a.is<EmitAction>()) {
    auto& ea = a.as<EmitAction>();
    if (!events_.count(ea.event_name)) {
      warn(a.pos, "Emitting undefined event: " + ea.event_name);
    }
  } else if (a.is<AssertAction>()) {
    check_expr(*a.as<AssertAction>().condition, scope);
  } else if (a.is<ExpectAction>()) {
    check_expr(*a.as<ExpectAction>().condition, scope);
  } else if (a.is<LogAction>()) {
    check_expr(*a.as<LogAction>().message, scope);
  } else if (a.is<IfAction>()) {
    auto& ia = a.as<IfAction>();
    check_expr(*ia.condition, scope);
    Scope s1; s1.parent = std::make_shared<Scope>(scope);
    check_actions(ia.then_body, s1);
    Scope s2; s2.parent = std::make_shared<Scope>(scope);
    check_actions(ia.else_body, s2);
  } else if (a.is<WhileAction>()) {
    auto& wa = a.as<WhileAction>();
    check_expr(*wa.condition, scope);
    Scope s; s.parent = std::make_shared<Scope>(scope);
    check_actions(wa.body, s);
  } else if (a.is<ForeachAction>()) {
    auto& fa = a.as<ForeachAction>();
    check_expr(*fa.collection, scope);
    Scope s; s.parent = std::make_shared<Scope>(scope);
    s.define(fa.var, Type::make_any(), a.pos);
    check_actions(fa.body, s);
  }
}

Type Sema::check_expr(const Expr& e, Scope& scope) {
  if (e.is<LiteralExpr>()) {
    auto& l = e.as<LiteralExpr>();
    switch (l.kind) {
      case LiteralExpr::Kind::Int:      return Type::make_int();
      case LiteralExpr::Kind::Float:    return Type::make_float();
      case LiteralExpr::Kind::Bool:     return Type::make_bool();
      case LiteralExpr::Kind::String:   return Type::make_string();
      case LiteralExpr::Kind::Duration: return Type::make_duration();
      case LiteralExpr::Kind::Null:     return Type::make_null();
    }
  }
  if (e.is<IdentExpr>()) {
    auto& id = e.as<IdentExpr>();
    auto* sym = scope.lookup(id.name);
    if (!sym) {
      // In EBP, many identifiers are entity types / runtime-resolved — don't error, just return any
      return Type::make_any();
    }
    return sym->type;
  }
  if (e.is<BinaryExpr>()) {
    auto& be = e.as<BinaryExpr>();
    Type lt = check_expr(*be.left,  scope);
    Type rt = check_expr(*be.right, scope);
    if (be.op == "==" || be.op == "!=" || be.op == "<" || be.op == ">" ||
        be.op == "<=" || be.op == ">=" || be.op == "&&" || be.op == "||") {
      return Type::make_bool();
    }
    if (lt.is_numeric() && rt.is_numeric()) {
      return (lt.kind == TypeKind::Float || rt.kind == TypeKind::Float)
        ? Type::make_float() : Type::make_int();
    }
    if (be.op == "+" && lt.kind == TypeKind::String) return Type::make_string();
    return Type::make_any();
  }
  if (e.is<UnaryExpr>()) {
    auto& ue = e.as<UnaryExpr>();
    Type t = check_expr(*ue.operand, scope);
    if (ue.op == "!") return Type::make_bool();
    return t;
  }
  if (e.is<MemberExpr>()) {
    check_expr(*e.as<MemberExpr>().obj, scope);
    return Type::make_any(); // runtime resolution
  }
  if (e.is<CallExpr>()) {
    auto& ce = e.as<CallExpr>();
    check_expr(*ce.callee, scope);
    for (auto& arg : ce.args) check_expr(*arg, scope);
    return Type::make_any();
  }
  return Type::make_any();
}

} // namespace yuspec::v1
