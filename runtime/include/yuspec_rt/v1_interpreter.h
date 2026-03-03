#pragma once
// YUSPEC v1.0 — Tree-Walking Interpreter
// The core execution engine for EBP programs
#include "yuspec_rt/v1_value.h"
#include "yuspec_rt/v1_ecs.h"
#include "yuspec_rt/v1_event_bus.h"
#include "yuspec_rt/v1_sm_runtime.h"
#include "yuspec/v1_ast.h"
#include <string>
#include <memory>
#include <vector>
#include <unordered_map>
#include <functional>
#include <sstream>
#include <optional>
#include <iostream>

namespace yuspec::v1::rt {

// ─── Execution signal (for break/continue/return/fail/transition) ─────────
struct Signal {
  enum class Kind { None, Break, Continue, Return, Fail, Transition } kind = Kind::None;
  Value       value;     // for Return / Fail message
  std::string state;     // for Transition
};

// ─── Native function type ─────────────────────────────────────────────────
using NativeFn = std::function<Value(std::vector<Value>)>;

// ─── Run configuration ────────────────────────────────────────────────────
struct RunConfig {
  int    max_ticks   = 10000;
  double tick_ms     = 16.0;      // 60 fps default
  bool   verbose     = false;
  bool   record_events = false;
  std::ostream* log_stream = nullptr;
};

// ─── Run result ───────────────────────────────────────────────────────────
struct RunResult {
  bool ok = true;
  std::string output;
  std::string errors;
  int assertions_passed = 0;
  int assertions_failed = 0;
};

// ─── Interpreter ──────────────────────────────────────────────────────────
class Interpreter {
public:
  Interpreter();

  // ── Register programs and native functions ──────────────────────────────
  void load_program(const Program& prog);
  void register_native(const std::string& name, NativeFn fn);

  // ── Execute a specific scenario or zone by name ─────────────────────────
  RunResult run_scenario(const std::string& name, const RunConfig& cfg = {});
  RunResult run_zone(const std::string& name, const RunConfig& cfg = {});
  RunResult run_all_scenarios(const RunConfig& cfg = {});

  // ── One-shot: evaluate expression in global scope ───────────────────────
  Value eval(const ExprPtr& expr);

  // ── Lower-level: used by SMInstance ────────────────────────────────────
  Signal exec_actions(const std::vector<ActionPtr>& actions, std::shared_ptr<Env> env);
  Signal exec_action(const Action& action, std::shared_ptr<Env> env);
  Value  eval_expr(const Expr& expr, std::shared_ptr<Env> env);

  // ── World / bus access ───────────────────────────────────────────────────
  World&    world()    { return world_; }
  EventBus& bus()      { return bus_; }
  SMManager& sm()      { return sm_; }
  std::shared_ptr<Env> global_env() { return global_; }

  // ── Logging ──────────────────────────────────────────────────────────────
  void log(const std::string& msg);
  std::string take_log();

  // ── Assertion tracking ────────────────────────────────────────────────────
  int assertions_passed = 0;
  int assertions_failed = 0;

private:
  World    world_;
  EventBus bus_;
  SMManager sm_;
  std::shared_ptr<Env> global_;
  std::ostringstream log_buf_;

  // Registered declarations from loaded programs
  std::unordered_map<std::string, const EntityDecl*>       entity_decls_;
  std::unordered_map<std::string, const BehaviorDecl*>     behavior_decls_;
  std::unordered_map<std::string, const EventDecl*>        event_decls_;
  std::unordered_map<std::string, const ComponentDecl*>    component_decls_;
  std::unordered_map<std::string, const WorkflowDecl*>     workflow_decls_;
  std::unordered_map<std::string, const ZoneDecl*>         zone_decls_;
  std::unordered_map<std::string, const SystemDecl*>       system_decls_;
  std::unordered_map<std::string, const ScenarioDecl*>     scenario_decls_;

  std::unordered_map<std::string, NativeFn> native_fns_;
  std::vector<std::unique_ptr<Program>> owned_programs_;

  // ── Internal helpers ────────────────────────────────────────────────────
  void register_declarations(const Program& prog);
  void init_zone(const ZoneDecl& zone, std::shared_ptr<Env> env);
  void register_stdlib();

  EntityId spawn_entity(const std::string& type, const std::string& tag,
                        const std::vector<std::pair<std::string, Value>>& props);
  void attach_behavior_to_entity(EntityId eid, const std::string& bname,
                                  std::shared_ptr<Env> parent_env);

  Value eval_binary(const std::string& op, Value left, Value right);
  Value call_native_or_method(const std::string& name, std::vector<Value> args,
                               std::shared_ptr<Env> env);

  // Set an lvalue (supports a.b.c chains)
  void set_lvalue(const Expr& lhs, Value val, std::shared_ptr<Env> env);
};

} // namespace yuspec::v1::rt
