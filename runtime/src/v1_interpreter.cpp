// YUSPEC v1.0 — Interpreter Implementation
#include "yuspec_rt/v1_interpreter.h"
#include <cmath>
#include <stdexcept>
#include <random>

namespace yuspec::v1::rt {

// ─── Static RNG ─────────────────────────────────────────────────────────
static std::mt19937& rng() {
  static std::mt19937 g(std::random_device{}());
  return g;
}

// ═══════════════════════════════════════════════════════════════════════════
// CONSTRUCTION
// ═══════════════════════════════════════════════════════════════════════════
Interpreter::Interpreter() : sm_(this, &bus_) {
  global_ = std::make_shared<Env>();
  register_stdlib();
}

void Interpreter::log(const std::string& msg)  { log_buf_ << msg << "\n"; }
std::string Interpreter::take_log() { std::string s = log_buf_.str(); log_buf_.str(""); return s; }

Value Interpreter::eval(const ExprPtr& expr) {
  return eval_expr(*expr, global_);
}

// ─── Standard library native functions ─────────────────────────────────
void Interpreter::register_stdlib() {
  // Math
  register_native("abs",   [](auto args){ return Value::from_float(std::abs(args[0].to_number())); });
  register_native("sqrt",  [](auto args){ return Value::from_float(std::sqrt(args[0].to_number())); });
  register_native("floor", [](auto args){ return Value::from_int((int64_t)std::floor(args[0].to_number())); });
  register_native("ceil",  [](auto args){ return Value::from_int((int64_t)std::ceil(args[0].to_number())); });
  register_native("min",   [](auto args){ return args[0] < args[1] ? args[0] : args[1]; });
  register_native("max",   [](auto args){ return args[0] > args[1] ? args[0] : args[1]; });
  register_native("clamp", [](auto args){
    Value lo = args[1], hi = args[2];
    if (args[0] < lo) return lo;
    if (args[0] > hi) return hi;
    return args[0];
  });

  // Random
  register_native("random", [](auto args) -> Value {
    double lo = args.empty() ? 0.0 : args[0].to_number();
    double hi = args.size() >= 2 ? args[1].to_number() : 1.0;
    std::uniform_real_distribution<double> d(lo, hi);
    return Value::from_float(d(rng()));
  });
  register_native("random_int", [](auto args) -> Value {
    int lo = args.empty() ? 0 : (int)args[0].as_int;
    int hi = args.size() >= 2 ? (int)args[1].as_int : 100;
    std::uniform_int_distribution<int> d(lo, hi);
    return Value::from_int(d(rng()));
  });

  // String utilities
  register_native("len", [](auto args) -> Value {
    auto& v = args[0];
    if (v.tag == Value::Tag::String) return Value::from_int((int64_t)v.as_string.size());
    if (v.tag == Value::Tag::List)   return Value::from_int((int64_t)v.as_list.size());
    if (v.tag == Value::Tag::Map)    return Value::from_int((int64_t)v.as_map.size());
    return Value::from_int(0);
  });
  register_native("str", [](auto args){ return Value::from_string(args[0].to_string()); });
  register_native("int", [](auto args) -> Value {
    if (args[0].tag == Value::Tag::Int) return args[0];
    return Value::from_int((int64_t)args[0].to_number());
  });
  register_native("float", [](auto args) -> Value {
    return Value::from_float(args[0].to_number());
  });

  // List utilities
  register_native("push", [](auto args) -> Value {
    if (args[0].tag != Value::Tag::List) throw std::runtime_error("push requires list");
    args[0].as_list.push_back(args[1]);
    return args[0];
  });
  register_native("contains", [](auto args) -> Value {
    if (args[0].tag == Value::Tag::List) {
      for (auto& e : args[0].as_list) if (e == args[1]) return Value::from_bool(true);
    }
    if (args[0].tag == Value::Tag::Map) {
      return Value::from_bool(args[0].as_map.count(args[1].to_string()) > 0);
    }
    return Value::from_bool(false);
  });

  // Type checking
  register_native("is_null",   [](auto a){ return Value::from_bool(a[0].is_null()); });
  register_native("is_entity", [](auto a){ return Value::from_bool(a[0].tag == Value::Tag::Entity); });
}

void Interpreter::register_native(const std::string& name, NativeFn fn) {
  native_fns_[name] = std::move(fn);
  global_->set(name, Value::from_string("<native:" + name + ">"));
}

// ─── Load program ─────────────────────────────────────────────────────────
void Interpreter::load_program(const Program& prog) {
  register_declarations(prog);
}

void Interpreter::register_declarations(const Program& prog) {
  for (auto& d : prog.declarations) {
    if (!d) continue;
    if (d->is<EntityDecl>()) {
      auto& e = d->as<EntityDecl>();
      entity_decls_[e.name] = &e;
    } else if (d->is<BehaviorDecl>()) {
      auto& b = d->as<BehaviorDecl>();
      behavior_decls_[b.name] = &b;
    } else if (d->is<EventDecl>()) {
      auto& ev = d->as<EventDecl>();
      event_decls_[ev.name] = &ev;
    } else if (d->is<ComponentDecl>()) {
      auto& c = d->as<ComponentDecl>();
      component_decls_[c.name] = &c;
    } else if (d->is<WorkflowDecl>()) {
      auto& w = d->as<WorkflowDecl>();
      workflow_decls_[w.name] = &w;
    } else if (d->is<ZoneDecl>()) {
      auto& z = d->as<ZoneDecl>();
      zone_decls_[z.name] = &z;
    } else if (d->is<SystemDecl>()) {
      auto& s = d->as<SystemDecl>();
      system_decls_[s.name] = &s;
    } else if (d->is<ScenarioDecl>()) {
      auto& sc = d->as<ScenarioDecl>();
      scenario_decls_[sc.name] = &sc;
    }
  }
}

// ─── Run scenario ────────────────────────────────────────────────────────
RunResult Interpreter::run_scenario(const std::string& name, const RunConfig& cfg) {
  RunResult result;
  auto it = scenario_decls_.find(name);
  if (it == scenario_decls_.end()) {
    result.ok = false;
    result.errors = "Scenario not found: " + name;
    return result;
  }

  bus_.enable_history(cfg.record_events);
  bus_.reset();
  bus_.enable_history(true);   // always record history for scenarios
  sm_.reset();
  world_.reset();
  assertions_passed = 0;
  assertions_failed = 0;

  auto env = std::make_shared<Env>(global_);
  Signal sig = exec_actions(it->second->steps, env);

  if (sig.kind == Signal::Kind::Fail) {
    result.ok = false;
    result.errors = "Scenario failed: " + sig.value.to_string();
  }
  result.output = take_log();
  result.assertions_passed = assertions_passed;
  result.assertions_failed = assertions_failed;
  if (assertions_failed > 0) result.ok = false;
  return result;
}

// ─── Run zone ────────────────────────────────────────────────────────────
RunResult Interpreter::run_zone(const std::string& name, const RunConfig& cfg) {
  RunResult result;
  auto it = zone_decls_.find(name);
  if (it == zone_decls_.end()) {
    result.ok = false;
    result.errors = "Zone not found: " + name;
    return result;
  }

  auto env = std::make_shared<Env>(global_);
  init_zone(*it->second, env);

  // Main loop
  for (int tick_n = 0; tick_n < cfg.max_ticks; tick_n++) {
    world_.tick(cfg.tick_ms);
    sm_.tick(cfg.tick_ms);
    bus_.flush();

    // Execute zone rules each tick
    for (auto& r : it->second->rules) {
      if (r.condition) {
        Value cv = eval_expr(*r.condition, env);
        if (cv.is_truthy()) exec_actions(r.actions, env);
      }
    }
  }

  result.output = take_log();
  result.ok = true;
  return result;
}

RunResult Interpreter::run_all_scenarios(const RunConfig& cfg) {
  RunResult combined; combined.ok = true;
  std::ostringstream out;
  for (auto& [name, _] : scenario_decls_) {
    auto r = run_scenario(name, cfg);
    out << "=== Scenario: " << name << " ===\n";
    out << r.output;
    out << "Assertions: " << r.assertions_passed << " passed, "
        << r.assertions_failed << " failed\n";
    out << (r.ok ? "PASS\n" : "FAIL: " + r.errors + "\n");
    combined.assertions_passed += r.assertions_passed;
    combined.assertions_failed += r.assertions_failed;
    if (!r.ok) combined.ok = false;
  }
  combined.output = out.str();
  return combined;
}

// ─── Zone initialization ─────────────────────────────────────────────────
void Interpreter::init_zone(const ZoneDecl& zone, std::shared_ptr<Env> env) {
  // Store terrain info
  if (zone.has_terrain) {
    env->set("terrain_w", Value::from_int(zone.terrain_w));
    env->set("terrain_h", Value::from_int(zone.terrain_h));
  }
  // Properties
  for (auto& pd : zone.properties) {
    Value v = pd.default_val ? eval_expr(**pd.default_val, env) : Value::null();
    env->set(pd.name, std::move(v));
  }
  // Spawns
  for (auto& sa : zone.spawns) {
    int count = 1;
    if (sa.count) {
      Value cv = eval_expr(**sa.count, env);
      count = (int)(cv.tag == Value::Tag::Int ? cv.as_int : (int64_t)cv.to_number());
    }
    std::vector<std::pair<std::string,Value>> props;
    for (auto& [k, ve] : sa.with_props) props.push_back({k, eval_expr(*ve, env)});

    for (int i = 0; i < count; i++) {
      EntityId eid = spawn_entity(sa.entity_type.name, "", props);
      // Auto-attach behaviors declared in entity def
      auto eit = entity_decls_.find(sa.entity_type.name);
      if (eit != entity_decls_.end()) {
        for (auto& bname : eit->second->has_behaviors) {
          attach_behavior_to_entity(eid, bname, env);
        }
      }
    }
  }
}

// ─── Entity spawning ─────────────────────────────────────────────────────
EntityId Interpreter::spawn_entity(const std::string& type, const std::string& tag,
                                    const std::vector<std::pair<std::string, Value>>& props) {
  EntityId eid = world_.create(type, tag);
  auto* e = world_.get(eid);
  if (!e) return eid;

  // Apply entity declaration defaults
  auto it = entity_decls_.find(type);
  if (it != entity_decls_.end()) {
    for (auto& pd : it->second->properties) {
      auto env = std::make_shared<Env>(global_);
      Value v = pd.default_val ? eval_expr(**pd.default_val, env) : Value::null();
      e->set_prop(pd.name, std::move(v));
    }
  }
  // Override with provided props
  for (auto& [k, v] : props) e->set_prop(k, v);

  return eid;
}

void Interpreter::attach_behavior_to_entity(EntityId eid, const std::string& bname,
                                              std::shared_ptr<Env> parent_env) {
  auto bit = behavior_decls_.find(bname);
  if (bit == behavior_decls_.end()) return;

  world_.attach_behavior(eid, bname);
  auto env = std::make_shared<Env>(global_);
  env->set("self", Value::from_entity(eid));

  // Initialize behavior properties with defaults
  for (auto& pd : bit->second->properties) {
    Value v = pd.default_val ? eval_expr(**pd.default_val, env) : Value::null();
    env->set(pd.name, std::move(v));
  }

  sm_.attach(bname, eid, bit->second, env);

  // Register event handlers from behavior
  for (auto& h : bit->second->handlers) {
    if (h.trigger.kind == TriggerKind::Event) {
      bus_.subscribe(h.trigger.event_name, [this, eid, &h, e=env](const Event& ev) {
        if (ev.target != NULL_ENTITY && ev.target != eid) return;
        e->set("event", Value::from_map(const_cast<ValueMap&>(ev.data)));
        e->set("self",  Value::from_entity(eid));
        exec_actions(h.actions, e);
      });
    }
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// ACTION EXECUTION
// ═══════════════════════════════════════════════════════════════════════════
Signal Interpreter::exec_actions(const std::vector<ActionPtr>& actions,
                                   std::shared_ptr<Env> env) {
  for (auto& a : actions) {
    if (!a) continue;
    Signal sig = exec_action(*a, env);
    if (sig.kind != Signal::Kind::None) return sig;
  }
  return {};
}

Signal Interpreter::exec_action(const Action& action, std::shared_ptr<Env> env) {
  if (action.is<SpawnAction>()) {
    auto& sa = action.as<SpawnAction>();
    int count = 1;
    if (sa.count) {
      Value cv = eval_expr(**sa.count, env);
      count = (int)(cv.tag == Value::Tag::Int ? cv.as_int : (int64_t)cv.to_number());
    }
    std::vector<std::pair<std::string,Value>> props;
    for (auto& [k, ve] : sa.with_props) props.push_back({k, eval_expr(*ve, env)});

    std::string tag_str;
    // Check placement for "tag" annotation
    if (sa.placement) {
      Value pv = eval_expr(**sa.placement, env);
      // "random" means random placement in terrain
      if (pv.tag == Value::Tag::String && pv.as_string == "random") {
        int tw = (int)env->get("terrain_w").as_int;
        int th = (int)env->get("terrain_h").as_int;
        if (tw > 0 && th > 0) {
          props.push_back({"x", Value::from_float((double)(rng()() % tw))});
          props.push_back({"y", Value::from_float((double)(rng()() % th))});
        }
      }
    }
    for (int i = 0; i < count; i++) {
      EntityId eid = spawn_entity(sa.entity_type.name, tag_str, props);
      auto eit = entity_decls_.find(sa.entity_type.name);
      if (eit != entity_decls_.end()) {
        for (auto& bname : eit->second->has_behaviors) attach_behavior_to_entity(eid, bname, env);
      }
    }
    return {};
  }

  if (action.is<EmitAction>()) {
    auto& ea = action.as<EmitAction>();
    Event ev;
    ev.name = ea.event_name;
    ev.time_ms = world_.elapsed_ms();
    for (auto& [k, ve] : ea.fields) ev.data[k] = eval_expr(*ve, env);
    if (ea.target) {
      Value tv = eval_expr(**ea.target, env);
      if (tv.tag == Value::Tag::Entity) ev.target = tv.entity_id;
    }
    auto src = env->get("self");
    if (src.tag == Value::Tag::Entity) ev.source = src.entity_id;
    bus_.emit_tracked(ev);   // copy — ev still valid below
    sm_.handle_event(ev);
    return {};
  }

  if (action.is<SetAction>()) {
    auto& sa = action.as<SetAction>();
    Value rhs = eval_expr(*sa.rhs, env);
    if (sa.op == "=") {
      set_lvalue(*sa.lhs, std::move(rhs), env);
    } else if (sa.op == "+=") {
      Value prev = eval_expr(*sa.lhs, env);
      set_lvalue(*sa.lhs, prev + rhs, env);
    } else if (sa.op == "-=") {
      Value prev = eval_expr(*sa.lhs, env);
      set_lvalue(*sa.lhs, prev - rhs, env);
    }
    return {};
  }

  if (action.is<LogAction>()) {
    Value msg = eval_expr(*action.as<LogAction>().message, env);
    log(msg.to_string());
    return {};
  }

  if (action.is<AssertAction>()) {
    auto& aa = action.as<AssertAction>();
    Value cv = eval_expr(*aa.condition, env);
    if (cv.is_truthy()) {
      assertions_passed++;
    } else {
      assertions_failed++;
      std::string msg = aa.message ? *aa.message : "Assertion failed";
      log("ASSERT FAILED: " + msg + " at line " + std::to_string(action.pos.line));
    }
    return {};
  }

  if (action.is<LetAction>()) {
    auto& la = action.as<LetAction>();
    Value v = eval_expr(*la.init, env);
    env->set(la.name, std::move(v));
    return {};
  }

  if (action.is<ExpectAction>()) {
    auto& ea = action.as<ExpectAction>();
    // Special case: if condition is a bare identifier, check event bus history
    bool passed = false;
    if (ea.condition && ea.condition->is<IdentExpr>()) {
      const std::string& ev_name = ea.condition->as<IdentExpr>().name;
      Value v = env->get(ev_name);
      if (v.tag == Value::Tag::Null) {
        // Treat as event name expectation
        passed = bus_.was_emitted(ev_name);
        if (!passed) log("EXPECT FAILED: event '" + ev_name + "' was never emitted (line " + std::to_string(action.pos.line) + ")");
      } else {
        passed = v.is_truthy();
        if (!passed) log("EXPECT FAILED at line " + std::to_string(action.pos.line));
      }
    } else {
      Value cv = eval_expr(*ea.condition, env);
      passed = cv.is_truthy();
      if (!passed) log("EXPECT FAILED at line " + std::to_string(action.pos.line));
    }
    if (passed) assertions_passed++; else assertions_failed++;
    return {};
  }

  if (action.is<WaitAction>()) {
    // In simulation: advance world time
    world_.tick(action.as<WaitAction>().dur_ms);
    sm_.tick(action.as<WaitAction>().dur_ms);
    bus_.flush();
    return {};
  }

  if (action.is<TransitionAction>()) {
    Signal s; s.kind = Signal::Kind::Transition;
    s.state = action.as<TransitionAction>().target_state;
    return s;
  }

  if (action.is<FailAction>()) {
    Signal s; s.kind = Signal::Kind::Fail;
    auto& fa = action.as<FailAction>();
    s.value = fa.message ? eval_expr(**fa.message, env) : Value::from_string("fail");
    return s;
  }

  if (action.is<ReturnAction>()) {
    Signal s; s.kind = Signal::Kind::Return;
    auto& ra = action.as<ReturnAction>();
    s.value = ra.value ? eval_expr(**ra.value, env) : Value::null();
    return s;
  }

  if (action.is<BreakAction>())    { Signal s; s.kind = Signal::Kind::Break;    return s; }
  if (action.is<ContinueAction>()) { Signal s; s.kind = Signal::Kind::Continue; return s; }

  if (action.is<DestroyAction>()) {
    Value tv = eval_expr(*action.as<DestroyAction>().target, env);
    if (tv.tag == Value::Tag::Entity) {
      sm_.detach_all(tv.entity_id);
      world_.destroy(tv.entity_id);
    }
    return {};
  }

  if (action.is<AttachAction>()) {
    auto& aa = action.as<AttachAction>();
    Value tv = eval_expr(*aa.target, env);
    EntityId eid = (tv.tag == Value::Tag::Entity) ? tv.entity_id : NULL_ENTITY;
    if (eid != NULL_ENTITY) attach_behavior_to_entity(eid, aa.behavior, env);
    return {};
  }

  if (action.is<DetachAction>()) {
    auto& da = action.as<DetachAction>();
    Value tv = eval_expr(*da.target, env);
    if (tv.tag == Value::Tag::Entity) {
      world_.detach_behavior(tv.entity_id, da.behavior);
      sm_.detach(da.behavior, tv.entity_id);
    }
    return {};
  }

  if (action.is<CallAction>()) {
    auto& ca = action.as<CallAction>();
    std::vector<Value> args;
    for (auto& a : ca.args) args.push_back(eval_expr(*a, env));
    eval_expr(*ca.callee, env); // side-effectful call
    return {};
  }

  if (action.is<IfAction>()) {
    auto& ia = action.as<IfAction>();
    Value cv = eval_expr(*ia.condition, env);
    auto sub = std::make_shared<Env>(env);
    if (cv.is_truthy()) return exec_actions(ia.then_body, sub);
    else                return exec_actions(ia.else_body, sub);
  }

  if (action.is<WhileAction>()) {
    auto& wa = action.as<WhileAction>();
    int guard = 100000;
    while (guard-- > 0) {
      Value cv = eval_expr(*wa.condition, env);
      if (!cv.is_truthy()) break;
      auto sub = std::make_shared<Env>(env);
      Signal sig = exec_actions(wa.body, sub);
      if (sig.kind == Signal::Kind::Break)    break;
      if (sig.kind == Signal::Kind::Continue) continue;
      if (sig.kind != Signal::Kind::None)     return sig;
    }
    return {};
  }

  if (action.is<ForeachAction>()) {
    auto& fa = action.as<ForeachAction>();
    Value col = eval_expr(*fa.collection, env);
    if (col.tag == Value::Tag::List) {
      for (auto& item : col.as_list) {
        auto sub = std::make_shared<Env>(env);
        sub->set(fa.var, item);
        Signal sig = exec_actions(fa.body, sub);
        if (sig.kind == Signal::Kind::Break)    break;
        if (sig.kind == Signal::Kind::Continue) continue;
        if (sig.kind != Signal::Kind::None)     return sig;
      }
    } else if (col.tag == Value::Tag::Map) {
      for (auto& [k, v] : col.as_map) {
        auto sub = std::make_shared<Env>(env);
        sub->set(fa.var, Value::from_string(k));
        sub->set(fa.var + "_val", v);
        Signal sig = exec_actions(fa.body, sub);
        if (sig.kind == Signal::Kind::Break)    break;
        if (sig.kind == Signal::Kind::Continue) continue;
        if (sig.kind != Signal::Kind::None)     return sig;
      }
    } else if (col.tag == Value::Tag::Entity) {
      // foreach entity in world of its type
      EntityId eid = col.entity_id;
      auto* e = world_.get(eid);
      if (e) {
        for (auto& [k, v] : e->props) {
          auto sub = std::make_shared<Env>(env);
          sub->set(fa.var, Value::from_string(k));
          Signal sig = exec_actions(fa.body, sub);
          if (sig.kind == Signal::Kind::Break)    break;
          if (sig.kind == Signal::Kind::Continue) continue;
          if (sig.kind != Signal::Kind::None)     return sig;
        }
      }
    }
    return {};
  }

  return {};
}

// ═══════════════════════════════════════════════════════════════════════════
// EXPRESSION EVALUATION
// ═══════════════════════════════════════════════════════════════════════════
Value Interpreter::eval_expr(const Expr& expr, std::shared_ptr<Env> env) {
  if (expr.is<LiteralExpr>()) {
    auto& l = expr.as<LiteralExpr>();
    switch (l.kind) {
      case LiteralExpr::Kind::Int:      return Value::from_int(l.int_val);
      case LiteralExpr::Kind::Float:    return Value::from_float(l.flt_val);
      case LiteralExpr::Kind::Bool:     return Value::from_bool(l.bool_val);
      case LiteralExpr::Kind::String:   return Value::from_string(l.str_val);
      case LiteralExpr::Kind::Duration: return Value::from_duration(l.dur_ms);
      case LiteralExpr::Kind::Null:     return Value::null();
    }
  }

  if (expr.is<IdentExpr>()) {
    auto& id = expr.as<IdentExpr>();
    // Check native functions
    if (native_fns_.count(id.name)) return Value::from_string("<native:" + id.name + ">");
    // Check env
    Value v = env->get(id.name);
    if (!v.is_null() || env->has(id.name)) return v;
    // Check entity type name
    if (entity_decls_.count(id.name)) return Value::from_string(id.name);
    return Value::null();
  }

  if (expr.is<MemberExpr>()) {
    auto& me = expr.as<MemberExpr>();
    Value obj = eval_expr(*me.obj, env);
    if (obj.tag == Value::Tag::Entity) {
      return world_.get_prop(obj.entity_id, me.field);
    }
    if (obj.tag == Value::Tag::Map) {
      auto it = obj.as_map.find(me.field);
      return (it != obj.as_map.end()) ? it->second : Value::null();
    }
    if (obj.tag == Value::Tag::String) {
      // string.length etc.
      if (me.field == "length" || me.field == "size")
        return Value::from_int((int64_t)obj.as_string.size());
    }
    if (obj.tag == Value::Tag::List) {
      if (me.field == "length" || me.field == "size")
        return Value::from_int((int64_t)obj.as_list.size());
    }
    return Value::null();
  }

  if (expr.is<IndexExpr>()) {
    auto& ie = expr.as<IndexExpr>();
    Value obj = eval_expr(*ie.obj, env);
    Value idx = eval_expr(*ie.index, env);
    if (obj.tag == Value::Tag::List) {
      int64_t i = idx.tag == Value::Tag::Int ? idx.as_int : (int64_t)idx.to_number();
      if (i >= 0 && i < (int64_t)obj.as_list.size()) return obj.as_list[(size_t)i];
      return Value::null();
    }
    if (obj.tag == Value::Tag::Map) {
      auto it = obj.as_map.find(idx.to_string());
      return (it != obj.as_map.end()) ? it->second : Value::null();
    }
    return Value::null();
  }

  if (expr.is<BinaryExpr>()) {
    auto& be = expr.as<BinaryExpr>();
    // Short-circuit for && and ||
    if (be.op == "&&") {
      Value lv = eval_expr(*be.left, env);
      if (!lv.is_truthy()) return Value::from_bool(false);
      return Value::from_bool(eval_expr(*be.right, env).is_truthy());
    }
    if (be.op == "||") {
      Value lv = eval_expr(*be.left, env);
      if (lv.is_truthy()) return Value::from_bool(true);
      return Value::from_bool(eval_expr(*be.right, env).is_truthy());
    }
    Value lv = eval_expr(*be.left,  env);
    Value rv = eval_expr(*be.right, env);
    return eval_binary(be.op, std::move(lv), std::move(rv));
  }

  if (expr.is<UnaryExpr>()) {
    auto& ue = expr.as<UnaryExpr>();
    Value v = eval_expr(*ue.operand, env);
    if (ue.op == "!")  return Value::from_bool(!v.is_truthy());
    if (ue.op == "-") {
      if (v.tag == Value::Tag::Int)   return Value::from_int(-v.as_int);
      if (v.tag == Value::Tag::Float) return Value::from_float(-v.as_float);
    }
    return v;
  }

  if (expr.is<CallExpr>()) {
    auto& ce = expr.as<CallExpr>();
    std::vector<Value> args;
    for (auto& a : ce.args) args.push_back(eval_expr(*a, env));

    // Direct function call
    if (ce.callee->is<IdentExpr>()) {
      std::string fname = ce.callee->as<IdentExpr>().name;
      return call_native_or_method(fname, std::move(args), env);
    }
    // Method call: obj.method(args)
    if (ce.callee->is<MemberExpr>()) {
      auto& me = ce.callee->as<MemberExpr>();
      Value obj = eval_expr(*me.obj, env);
      args.insert(args.begin(), obj);
      return call_native_or_method(me.field, std::move(args), env);
    }
    return Value::null();
  }

  if (expr.is<ListExpr>()) {
    ValueList l;
    for (auto& e : expr.as<ListExpr>().elements) l.push_back(eval_expr(*e, env));
    return Value::from_list(std::move(l));
  }

  if (expr.is<MapExpr>()) {
    ValueMap m;
    for (auto& [k, v] : expr.as<MapExpr>().entries) {
      m[eval_expr(*k, env).to_string()] = eval_expr(*v, env);
    }
    return Value::from_map(std::move(m));
  }

  return Value::null();
}

Value Interpreter::eval_binary(const std::string& op, Value left, Value right) {
  if (op == "+")  return left + right;
  if (op == "-")  return left - right;
  if (op == "*")  return left * right;
  if (op == "/")  return left / right;
  if (op == "%")  return left % right;
  if (op == "==") return Value::from_bool(left == right);
  if (op == "!=") return Value::from_bool(left != right);
  if (op == "<")  return Value::from_bool(left <  right);
  if (op == ">")  return Value::from_bool(left >  right);
  if (op == "<=") return Value::from_bool(left <= right);
  if (op == ">=") return Value::from_bool(left >= right);
  return Value::null();
}

Value Interpreter::call_native_or_method(const std::string& name,
                                           std::vector<Value> args,
                                           std::shared_ptr<Env> env) {
  // Native function
  auto nit = native_fns_.find(name);
  if (nit != native_fns_.end()) return nit->second(std::move(args));

  // Built-in methods
  if (name == "entities_of" && !args.empty()) {
    std::string type = args[0].to_string();
    auto ids = world_.by_type(type);
    ValueList l;
    for (auto id : ids) l.push_back(Value::from_entity(id));
    return Value::from_list(std::move(l));
  }
  if (name == "count" && !args.empty()) {
    std::string type = args[0].to_string();
    return Value::from_int(world_.count(type));
  }
  if (name == "count_entities") {
    return Value::from_int(world_.count());
  }
  if (name == "has_behavior" && args.size() >= 2) {
    EntityId eid = args[0].tag == Value::Tag::Entity ? args[0].entity_id : NULL_ENTITY;
    return Value::from_bool(world_.has_behavior(eid, args[1].to_string()));
  }
  if (name == "elapsed") {
    return Value::from_duration(world_.elapsed_ms());
  }

  // Unknown — return null (permissive in EBP)
  return Value::null();
}

void Interpreter::set_lvalue(const Expr& lhs, Value val, std::shared_ptr<Env> env) {
  if (lhs.is<IdentExpr>()) {
    env->assign(lhs.as<IdentExpr>().name, std::move(val));
    return;
  }
  if (lhs.is<MemberExpr>()) {
    auto& me = lhs.as<MemberExpr>();
    Value obj = eval_expr(*me.obj, env);
    if (obj.tag == Value::Tag::Entity) {
      world_.set_prop(obj.entity_id, me.field, std::move(val));
      return;
    }
    // For maps stored in env: get object, mutate, store back
    if (me.obj->is<IdentExpr>()) {
      std::string oname = me.obj->as<IdentExpr>().name;
      Value existing = env->get(oname);
      if (existing.tag == Value::Tag::Map) {
        existing.as_map[me.field] = std::move(val);
        env->assign(oname, std::move(existing));
      }
    }
    return;
  }
  if (lhs.is<IndexExpr>()) {
    auto& ie = lhs.as<IndexExpr>();
    if (ie.obj->is<IdentExpr>()) {
      std::string oname = ie.obj->as<IdentExpr>().name;
      Value existing = env->get(oname);
      Value idx = eval_expr(*ie.index, env);
      if (existing.tag == Value::Tag::List) {
        int64_t i = idx.as_int;
        if (i >= 0 && i < (int64_t)existing.as_list.size())
          existing.as_list[(size_t)i] = std::move(val);
        env->assign(oname, std::move(existing));
      } else if (existing.tag == Value::Tag::Map) {
        existing.as_map[idx.to_string()] = std::move(val);
        env->assign(oname, std::move(existing));
      }
    }
  }
}

} // namespace yuspec::v1::rt
