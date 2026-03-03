#pragma once
// YUSPEC v1.0 — State Machine Runtime
// Executes BehaviorDecl / StateMachineDecl definitions at runtime
#include "yuspec_rt/v1_value.h"
#include "yuspec_rt/v1_ecs.h"
#include "yuspec_rt/v1_event_bus.h"
#include "yuspec/v1_ast.h"
#include <string>
#include <vector>
#include <memory>
#include <optional>
#include <functional>
#include <unordered_map>

namespace yuspec::v1::rt {

// Forward declare interpreter for action execution
class Interpreter;

// ─── Instance of a state machine running on one entity ───────────────────
class SMInstance {
public:
  struct Config {
    std::string              behavior_name;
    EntityId                 owner = NULL_ENTITY;
    const BehaviorDecl*      def   = nullptr;       // from compiled AST
    std::shared_ptr<Env>     env;                   // per-instance variables
  };

  explicit SMInstance(Config cfg, Interpreter* interp);

  // ── Lifecycle ────────────────────────────────────────────────────────────
  void start();
  void tick(double dt_ms);
  void handle_event(const Event& ev);
  void force_transition(const std::string& target_state);
  void cancel();

  const std::string& current_state() const { return current_state_; }
  bool is_done() const { return done_; }
  const std::string& behavior_name() const { return cfg_.behavior_name; }
  EntityId owner() const { return cfg_.owner; }

  // Called by interpreter to signal an explicit transition action
  void request_transition(const std::string& state);

private:
  Config       cfg_;
  Interpreter* interp_;
  std::string  current_state_;
  double       state_elapsed_ms_ = 0.0;
  int          retry_count_      = 0;
  bool         done_             = false;
  bool         pending_transition_ = false;
  std::string  pending_state_;

  const StateDecl* find_state(const std::string& name) const;
  const TransitionDecl* find_transition(const std::string& trigger_name,
                                         const std::string& from_state) const;
  void enter_state(const std::string& name);
  void exit_state(const std::string& name);
  void execute_actions(const std::vector<ActionPtr>& actions);
  bool eval_condition(const ExprPtr& cond) const;
};

// ─── SM Manager: all running instances ───────────────────────────────────
class SMManager {
public:
  explicit SMManager(Interpreter* interp, EventBus* bus);

  // Attach a behavior to an entity — creates a running SMInstance
  void attach(const std::string& behavior_name,
              EntityId owner,
              const BehaviorDecl* def,
              std::shared_ptr<Env> env);

  void detach(const std::string& behavior_name, EntityId owner);
  void detach_all(EntityId owner);
  void reset() { instances_.clear(); } // clears all running state machines

  void tick(double dt_ms);
  void handle_event(const Event& ev);

  SMInstance* find(const std::string& behavior, EntityId owner);

private:
  Interpreter* interp_;
  EventBus*    bus_;
  std::vector<std::unique_ptr<SMInstance>> instances_;
};

} // namespace yuspec::v1::rt
