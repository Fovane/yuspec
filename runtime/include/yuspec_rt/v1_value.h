#pragma once
// YUSPEC v1.0 — Runtime Value System
// Dynamically-typed value used during interpretation
#include <string>
#include <vector>
#include <unordered_map>
#include <memory>
#include <variant>
#include <stdexcept>
#include <sstream>

namespace yuspec::v1::rt {

struct Value;
using ValuePtr = std::shared_ptr<Value>;
using ValueMap = std::unordered_map<std::string, Value>;
using ValueList = std::vector<Value>;

struct NullType {};

struct Value {
  using Data = std::variant<
    NullType,
    int64_t,
    double,
    bool,
    std::string,
    double,           // duration in ms — use int index 6
    ValueList,
    ValueMap,
    int32_t           // entity_id — positive means live entity
  >;

  // Because double appears twice in variant, we use a tagged union instead:
  enum class Tag { Null, Int, Float, Bool, String, Duration, List, Map, Entity };
  Tag tag = Tag::Null;

  int64_t     as_int  = 0;
  double      as_float = 0.0;
  bool        as_bool  = false;
  std::string as_string;
  double      as_duration_ms = 0.0;       // Tag::Duration
  ValueList   as_list;
  ValueMap    as_map;
  int32_t     entity_id = 0;              // Tag::Entity

  // ── Factories ──────────────────────────────────────────────────────────
  static Value null()                      { Value v; v.tag = Tag::Null; return v; }
  static Value from_int(int64_t i)         { Value v; v.tag = Tag::Int;    v.as_int = i; return v; }
  static Value from_float(double f)        { Value v; v.tag = Tag::Float;  v.as_float = f; return v; }
  static Value from_bool(bool b)           { Value v; v.tag = Tag::Bool;   v.as_bool = b; return v; }
  static Value from_string(std::string s)  { Value v; v.tag = Tag::String; v.as_string = std::move(s); return v; }
  static Value from_duration(double ms)    { Value v; v.tag = Tag::Duration; v.as_duration_ms = ms; return v; }
  static Value from_list(ValueList l)      { Value v; v.tag = Tag::List;   v.as_list = std::move(l); return v; }
  static Value from_map(ValueMap m)        { Value v; v.tag = Tag::Map;    v.as_map = std::move(m); return v; }
  static Value from_entity(int32_t id)     { Value v; v.tag = Tag::Entity; v.entity_id = id; return v; }

  // ── Conversions ─────────────────────────────────────────────────────────
  bool is_null()     const { return tag == Tag::Null; }
  bool is_truthy()   const {
    switch (tag) {
      case Tag::Null:    return false;
      case Tag::Bool:    return as_bool;
      case Tag::Int:     return as_int != 0;
      case Tag::Float:   return as_float != 0.0;
      case Tag::String:  return !as_string.empty();
      case Tag::Entity:  return entity_id != 0;
      default:           return true;
    }
  }
  double to_number() const {
    if (tag == Tag::Int)   return (double)as_int;
    if (tag == Tag::Float) return as_float;
    throw std::runtime_error("Value is not a number");
  }
  std::string to_string() const {
    std::ostringstream oss;
    switch (tag) {
      case Tag::Null:     return "null";
      case Tag::Bool:     return as_bool ? "true" : "false";
      case Tag::Int:      return std::to_string(as_int);
      case Tag::Float:    oss << as_float; return oss.str();
      case Tag::String:   return as_string;
      case Tag::Duration: oss << as_duration_ms << "ms"; return oss.str();
      case Tag::Entity:   return "entity#" + std::to_string(entity_id);
      case Tag::List: {
        std::string s = "[";
        for (size_t i=0; i<as_list.size(); i++) {
          if (i) s += ", ";
          s += as_list[i].to_string();
        }
        return s + "]";
      }
      case Tag::Map: {
        std::string s = "{";
        bool first = true;
        for (auto& [k,v] : as_map) {
          if (!first) s += ", "; first = false;
          s += k + ": " + v.to_string();
        }
        return s + "}";
      }
    }
    return "?";
  }

  // ── Comparison ──────────────────────────────────────────────────────────
  bool operator==(const Value& o) const {
    if (tag != o.tag) {
      // numeric coercion
      if ((tag == Tag::Int || tag == Tag::Float) &&
          (o.tag == Tag::Int || o.tag == Tag::Float)) {
        return to_number() == o.to_number();
      }
      return false;
    }
    switch (tag) {
      case Tag::Null:    return true;
      case Tag::Bool:    return as_bool == o.as_bool;
      case Tag::Int:     return as_int == o.as_int;
      case Tag::Float:   return as_float == o.as_float;
      case Tag::String:  return as_string == o.as_string;
      case Tag::Entity:  return entity_id == o.entity_id;
      default:           return false;
    }
  }
  bool operator!=(const Value& o) const { return !(*this == o); }
  bool operator<(const Value& o)  const {
    if (tag == Tag::Int && o.tag == Tag::Int) return as_int < o.as_int;
    return to_number() < o.to_number();
  }
  bool operator>(const Value& o)  const { return o < *this; }
  bool operator<=(const Value& o) const { return !(o < *this); }
  bool operator>=(const Value& o) const { return !(*this < o); }

  // ── Arithmetic ──────────────────────────────────────────────────────────
  Value operator+(const Value& o) const {
    if (tag == Tag::String || o.tag == Tag::String)
      return Value::from_string(to_string() + o.to_string());
    if (tag == Tag::Int && o.tag == Tag::Int)
      return Value::from_int(as_int + o.as_int);
    return Value::from_float(to_number() + o.to_number());
  }
  Value operator-(const Value& o) const {
    if (tag == Tag::Int && o.tag == Tag::Int) return Value::from_int(as_int - o.as_int);
    return Value::from_float(to_number() - o.to_number());
  }
  Value operator*(const Value& o) const {
    if (tag == Tag::Int && o.tag == Tag::Int) return Value::from_int(as_int * o.as_int);
    return Value::from_float(to_number() * o.to_number());
  }
  Value operator/(const Value& o) const {
    double d = o.to_number();
    if (d == 0.0) throw std::runtime_error("Division by zero");
    if (tag == Tag::Int && o.tag == Tag::Int) return Value::from_int(as_int / o.as_int);
    return Value::from_float(to_number() / d);
  }
  Value operator%(const Value& o) const {
    if (tag == Tag::Int && o.tag == Tag::Int) return Value::from_int(as_int % o.as_int);
    throw std::runtime_error("% requires integer operands");
  }
};

// ─── Runtime environment (variable scope) ────────────────────────────────
class Env {
public:
  std::shared_ptr<Env> parent;
  std::unordered_map<std::string, Value> vars;

  Env() = default;
  explicit Env(std::shared_ptr<Env> p) : parent(std::move(p)) {}

  void set(const std::string& name, Value val) { vars[name] = std::move(val); }
  Value get(const std::string& name) const {
    auto it = vars.find(name);
    if (it != vars.end()) return it->second;
    if (parent) return parent->get(name);
    return Value::null();
  }
  bool has(const std::string& name) const {
    return vars.count(name) || (parent && parent->has(name));
  }
  // Set in the scope that owns the variable (walk up)
  void assign(const std::string& name, Value val) {
    auto it = vars.find(name);
    if (it != vars.end()) { it->second = std::move(val); return; }
    if (parent) { parent->assign(name, std::move(val)); return; }
    vars[name] = std::move(val); // define at global
  }
};

} // namespace yuspec::v1::rt
