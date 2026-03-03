#pragma once
// YUSPEC v1.0 — Event Bus
// Publish-subscribe event system for EBP runtime
#include "yuspec_rt/v1_value.h"
#include "yuspec_rt/v1_ecs.h"
#include <functional>
#include <vector>
#include <unordered_map>
#include <queue>
#include <string>
#include <optional>

namespace yuspec::v1::rt {

struct Event {
  std::string name;
  ValueMap    data;          // event payload fields
  EntityId    source  = NULL_ENTITY;  // who emitted it
  EntityId    target  = NULL_ENTITY;  // directed to specific entity (0 = broadcast)
  double      time_ms = 0.0;

  Value field(const std::string& key) const {
    auto it = data.find(key);
    return (it != data.end()) ? it->second : Value::null();
  }
};

using EventHandler = std::function<void(const Event&)>;

class EventBus {
public:
  // ── Subscribe ────────────────────────────────────────────────────────────
  int subscribe(const std::string& event_name, EventHandler handler) {
    int id = next_sub_id_++;
    subs_[event_name].push_back({id, std::move(handler)});
    return id;
  }

  void unsubscribe(const std::string& event_name, int sub_id) {
    auto it = subs_.find(event_name);
    if (it == subs_.end()) return;
    auto& v = it->second;
    v.erase(std::remove_if(v.begin(), v.end(),
      [sub_id](const Sub& s){ return s.id == sub_id; }), v.end());
  }

  // ── Publish ──────────────────────────────────────────────────────────────
  // Immediate dispatch — synchronous
  void emit(Event ev) {
    auto it = subs_.find(ev.name);
    if (it == subs_.end()) return;
    for (auto& sub : it->second) sub.handler(ev);
  }

  // Deferred dispatch — pushed to queue, processed on flush()
  void emit_deferred(Event ev) {
    queue_.push(std::move(ev));
  }

  void flush() {
    while (!queue_.empty()) {
      Event ev = std::move(queue_.front()); queue_.pop();
      emit(std::move(ev));
    }
  }

  // ── History (for assertions / scenarios) ─────────────────────────────────
  void enable_history(bool on) { record_history_ = on; }
  const std::vector<Event>& history() const { return history_; }
  void clear_history() { history_.clear(); }

  void reset() {
    subs_.clear();
    while (!queue_.empty()) queue_.pop();
    history_.clear();
    record_history_ = false;
  }

  bool was_emitted(const std::string& name) const {
    return std::any_of(history_.begin(), history_.end(),
      [&](const Event& e){ return e.name == name; });
  }
  std::optional<Event> last(const std::string& name) const {
    for (int i = (int)history_.size()-1; i >= 0; i--) {
      if (history_[i].name == name) return history_[i];
    }
    return std::nullopt;
  }

private:
  struct Sub { int id; EventHandler handler; };
  std::unordered_map<std::string, std::vector<Sub>> subs_;
  std::queue<Event> queue_;
  int next_sub_id_ = 1;
  bool record_history_ = false;
  std::vector<Event> history_;

  // Wrap emission through history recorder
public:
  void emit_tracked(Event ev) {
    if (record_history_) history_.push_back(ev);
    emit(std::move(ev));
  }
};

} // namespace yuspec::v1::rt
