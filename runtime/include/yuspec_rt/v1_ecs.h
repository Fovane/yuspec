#pragma once
// YUSPEC v1.0 — Entity Component System
// Entities are dynamic property bags + behavior instances
#include "yuspec_rt/v1_value.h"
#include <unordered_map>
#include <unordered_set>
#include <vector>
#include <string>
#include <functional>
#include <memory>
#include <algorithm>

namespace yuspec::v1::rt {

using EntityId = int32_t;
static constexpr EntityId NULL_ENTITY = 0;

// ─── Entity ───────────────────────────────────────────────────────────────
struct Entity {
  EntityId    id   = NULL_ENTITY;
  std::string type;                         // entity type name
  std::string tag;                          // optional tag
  bool        active = true;

  ValueMap    props;                         // runtime properties
  std::unordered_set<std::string> behaviors; // attached behavior names
  std::unordered_set<std::string> components;

  Value get_prop(const std::string& key) const {
    auto it = props.find(key);
    return (it != props.end()) ? it->second : Value::null();
  }
  void set_prop(const std::string& key, Value v) { props[key] = std::move(v); }

  // Convenience
  double get_float(const std::string& key, double def = 0.0) const {
    auto v = get_prop(key);
    if (v.tag == Value::Tag::Float) return v.as_float;
    if (v.tag == Value::Tag::Int)   return (double)v.as_int;
    return def;
  }
  int64_t get_int(const std::string& key, int64_t def = 0) const {
    auto v = get_prop(key);
    if (v.tag == Value::Tag::Int)   return v.as_int;
    if (v.tag == Value::Tag::Float) return (int64_t)v.as_float;
    return def;
  }
  bool get_bool(const std::string& key, bool def = false) const {
    auto v = get_prop(key);
    if (v.tag == Value::Tag::Bool) return v.as_bool;
    return def;
  }
};

// ─── World (ECS core) ─────────────────────────────────────────────────────
class World {
public:
  // ── Entity lifecycle ────────────────────────────────────────────────────
  EntityId create(const std::string& type, const std::string& tag = "") {
    EntityId id = ++next_id_;
    Entity e; e.id = id; e.type = type; e.tag = tag;
    entities_[id] = std::move(e);
    by_type_[type].push_back(id);
    if (!tag.empty()) by_tag_[tag].push_back(id);
    return id;
  }

  void destroy(EntityId id) {
    auto it = entities_.find(id);
    if (it == entities_.end()) return;
    auto& e = it->second;
    auto& tv = by_type_[e.type];
    tv.erase(std::remove(tv.begin(), tv.end(), id), tv.end());
    if (!e.tag.empty()) {
      auto& tgv = by_tag_[e.tag];
      tgv.erase(std::remove(tgv.begin(), tgv.end(), id), tgv.end());
    }
    entities_.erase(it);
    dead_.push_back(id);
  }

  Entity* get(EntityId id) {
    auto it = entities_.find(id);
    return (it != entities_.end()) ? &it->second : nullptr;
  }
  const Entity* get(EntityId id) const {
    auto it = entities_.find(id);
    return (it != entities_.end()) ? &it->second : nullptr;
  }

  // ── Property access ──────────────────────────────────────────────────────
  Value get_prop(EntityId id, const std::string& key) const {
    auto* e = get(id);
    return e ? e->get_prop(key) : Value::null();
  }
  void set_prop(EntityId id, const std::string& key, Value val) {
    auto* e = get(id);
    if (e) e->set_prop(key, std::move(val));
  }

  // ── Queries ──────────────────────────────────────────────────────────────
  const std::vector<EntityId>& by_type(const std::string& type) const {
    static const std::vector<EntityId> empty;
    auto it = by_type_.find(type);
    return (it != by_type_.end()) ? it->second : empty;
  }
  const std::vector<EntityId>& by_tag(const std::string& tag) const {
    static const std::vector<EntityId> empty;
    auto it = by_tag_.find(tag);
    return (it != by_tag_.end()) ? it->second : empty;
  }
  std::vector<EntityId> all_entities() const {
    std::vector<EntityId> ids;
    ids.reserve(entities_.size());
    for (auto& [id, _] : entities_) ids.push_back(id);
    return ids;
  }
  int count(const std::string& type = "") const {
    if (type.empty()) return (int)entities_.size();
    auto it = by_type_.find(type);
    return (it != by_type_.end()) ? (int)it->second.size() : 0;
  }

  // ── Behavior attachment ──────────────────────────────────────────────────
  void attach_behavior(EntityId id, const std::string& bname) {
    auto* e = get(id);
    if (e) e->behaviors.insert(bname);
  }
  void detach_behavior(EntityId id, const std::string& bname) {
    auto* e = get(id);
    if (e) e->behaviors.erase(bname);
  }
  bool has_behavior(EntityId id, const std::string& bname) const {
    auto* e = get(id);
    return e && e->behaviors.count(bname);
  }

  // ── Tick registry: systems register tick callbacks ───────────────────────
  using TickFn = std::function<void(World&, double dt)>;
  void register_tick(std::string name, TickFn fn) {
    tick_fns_.push_back({std::move(name), std::move(fn)});
  }
  void tick(double dt_ms) {
    elapsed_ms_ += dt_ms;
    for (auto& [nm, fn] : tick_fns_) fn(*this, dt_ms);
  }

  double elapsed_ms() const { return elapsed_ms_; }
  EntityId next_id()  const { return next_id_; }

  // ── Scenario isolation: clears all entities and timeline (keeps tick_fns) ─
  void reset() {
    entities_.clear();
    by_type_.clear();
    by_tag_.clear();
    dead_.clear();
    elapsed_ms_ = 0.0;
    // Note: tick_fns_ intentionally kept (system-level callbacks)
  }

private:
  EntityId next_id_ = 0;
  double   elapsed_ms_ = 0.0;
  std::unordered_map<EntityId, Entity> entities_;
  std::unordered_map<std::string, std::vector<EntityId>> by_type_;
  std::unordered_map<std::string, std::vector<EntityId>> by_tag_;
  std::vector<EntityId> dead_;
  std::vector<std::pair<std::string, TickFn>> tick_fns_;
};

} // namespace yuspec::v1::rt
