// YUSPEC v1.0 — State Machine Runtime Implementation
#include "yuspec_rt/v1_sm_runtime.h"
#include "yuspec_rt/v1_interpreter.h"
#include <algorithm>
#include <stdexcept>

namespace yuspec::v1::rt {

// ═══════════════════════════════════════════════════════════════════════════
// SMInstance
// ═══════════════════════════════════════════════════════════════════════════
SMInstance::SMInstance(Config cfg, Interpreter* interp)
  : cfg_(std::move(cfg)), interp_(interp) {}

void SMInstance::start() {
  // Find initial state
  if (!cfg_.def) return;
  for (auto& s : cfg_.def->states) {
    if (s.is_initial) { enter_state(s.name); return; }
  }
  if (!cfg_.def->states.empty()) {
    enter_state(cfg_.def->states[0].name); // fallback: first state
  }
}

void SMInstance::tick(double dt_ms) {
  if (done_) return;
  state_elapsed_ms_ += dt_ms;

  // Check timeout transitions from current state
  if (cfg_.def) {
    for (auto& td : cfg_.def->transitions) {
      // from_state matches or wildcard
      bool from_ok = !td.from_state || *td.from_state == current_state_;
      if (!from_ok) continue;
      if (td.from_trigger.kind == TriggerKind::Timeout) {
        // find timeout_ms from current state
        auto* sd = find_state(current_state_);
        if (sd && sd->timeout_ms && state_elapsed_ms_ >= *sd->timeout_ms) {
          // Check retry
          if (sd->retry_count > 0 && retry_count_ < sd->retry_count) {
            retry_count_++;
            state_elapsed_ms_ = 0.0;
            execute_actions(td.actions);
            return;
          }
          execute_actions(td.actions);
          enter_state(td.to_state);
          return;
        }
      }
      // Condition-based transitions (polled every tick)
      if (td.from_trigger.kind == TriggerKind::Condition && td.from_trigger.condition) {
        Signal sig = interp_->exec_actions({}, cfg_.env);
        Value cv = interp_->eval_expr(*td.from_trigger.condition, cfg_.env);
        if (cv.is_truthy()) {
          execute_actions(td.actions);
          enter_state(td.to_state);
          return;
        }
      }
    }
    // Check rules
    for (auto& r : cfg_.def->rules) {
      if (r.condition) {
        Value cv = interp_->eval_expr(*r.condition, cfg_.env);
        if (cv.is_truthy()) {
          execute_actions(r.actions);
        }
      }
    }
  }

  // Handle pending transition from an action
  if (pending_transition_) {
    pending_transition_ = false;
    enter_state(pending_state_);
  }
}

void SMInstance::handle_event(const Event& ev) {
  if (done_ || !cfg_.def) return;

  // Check transitions triggered by this event
  for (auto& td : cfg_.def->transitions) {
    bool from_ok = !td.from_state || *td.from_state == current_state_;
    if (!from_ok) continue;
    if (td.from_trigger.kind == TriggerKind::Event &&
        td.from_trigger.event_name == ev.name) {
      // Inject event fields into env
      cfg_.env->set("event", Value::from_map(const_cast<ValueMap&>(ev.data)));
      execute_actions(td.actions);
      enter_state(td.to_state);
      return;
    }
  }
  // Check handlers
  for (auto& h : cfg_.def->handlers) {
    if (h.trigger.kind == TriggerKind::Event && h.trigger.event_name == ev.name) {
      cfg_.env->set("event", Value::from_map(const_cast<ValueMap&>(ev.data)));
      execute_actions(h.actions);
      if (pending_transition_) {
        pending_transition_ = false;
        enter_state(pending_state_);
      }
      return;
    }
  }
}

void SMInstance::force_transition(const std::string& target_state) {
  enter_state(target_state);
}

void SMInstance::request_transition(const std::string& state) {
  pending_transition_ = true;
  pending_state_ = state;
}

void SMInstance::cancel() { done_ = true; }

const StateDecl* SMInstance::find_state(const std::string& name) const {
  if (!cfg_.def) return nullptr;
  for (auto& s : cfg_.def->states) {
    if (s.name == name) return &s;
  }
  return nullptr;
}

void SMInstance::enter_state(const std::string& name) {
  // Exit current state
  if (!current_state_.empty()) exit_state(current_state_);

  current_state_    = name;
  state_elapsed_ms_ = 0.0;
  retry_count_      = 0;

  auto* sd = find_state(name);
  if (sd) {
    // Execute on_enter actions
    execute_actions(sd->on_enter);
    // Handle transitions triggered during on_enter
    if (pending_transition_) {
      pending_transition_ = false;
      enter_state(pending_state_);
      return;
    }
    // Check if terminal
    if (sd->is_terminal) done_ = true;
  }
}

void SMInstance::exit_state(const std::string& name) {
  auto* sd = find_state(name);
  if (sd) execute_actions(sd->on_exit);
}

void SMInstance::execute_actions(const std::vector<ActionPtr>& actions) {
  if (!cfg_.env) return;
  cfg_.env->set("self", Value::from_entity(cfg_.owner));
  Signal sig = interp_->exec_actions(actions, cfg_.env);
  if (sig.kind == Signal::Kind::Transition && !sig.state.empty()) {
    pending_transition_ = true;
    pending_state_      = sig.state;
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// SMManager
// ═══════════════════════════════════════════════════════════════════════════
SMManager::SMManager(Interpreter* interp, EventBus* bus)
  : interp_(interp), bus_(bus) {
  // Subscribe to all events and forward to running instances
  // (done lazily in attach)
}

void SMManager::attach(const std::string& behavior_name,
                        EntityId owner,
                        const BehaviorDecl* def,
                        std::shared_ptr<Env> env) {
  SMInstance::Config cfg;
  cfg.behavior_name = behavior_name;
  cfg.owner = owner;
  cfg.def   = def;
  cfg.env   = env ? env : std::make_shared<Env>();
  cfg.env->set("self", Value::from_entity(owner));

  auto inst = std::make_unique<SMInstance>(std::move(cfg), interp_);
  inst->start();
  instances_.push_back(std::move(inst));
}

void SMManager::detach(const std::string& behavior_name, EntityId owner) {
  instances_.erase(
    std::remove_if(instances_.begin(), instances_.end(),
      [&](const auto& i){ return i->behavior_name() == behavior_name && i->owner() == owner; }),
    instances_.end());
}

void SMManager::detach_all(EntityId owner) {
  instances_.erase(
    std::remove_if(instances_.begin(), instances_.end(),
      [owner](const auto& i){ return i->owner() == owner; }),
    instances_.end());
}

void SMManager::tick(double dt_ms) {
  for (auto& inst : instances_) inst->tick(dt_ms);
  // Remove done instances
  instances_.erase(
    std::remove_if(instances_.begin(), instances_.end(),
      [](const auto& i){ return i->is_done(); }),
    instances_.end());
}

void SMManager::handle_event(const Event& ev) {
  for (auto& inst : instances_) inst->handle_event(ev);
}

SMInstance* SMManager::find(const std::string& behavior, EntityId owner) {
  for (auto& inst : instances_) {
    if (inst->behavior_name() == behavior && inst->owner() == owner)
      return inst.get();
  }
  return nullptr;
}

} // namespace yuspec::v1::rt
