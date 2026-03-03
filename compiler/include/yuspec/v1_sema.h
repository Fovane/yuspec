#pragma once
// YUSPEC v1.0 — Type System + Symbol Table + Semantic Analyzer
#include "yuspec/v1_ast.h"
#include "yuspec/v1_lexer.h"
#include <unordered_map>
#include <string>
#include <vector>
#include <memory>
#include <algorithm>

namespace yuspec::v1 {

// ═══════════════════════════════════════════════════════════════════════════
// TYPE SYSTEM
// ═══════════════════════════════════════════════════════════════════════════
enum class TypeKind {
  Int, Float, Bool, String, Duration, Null, Any, Void,
  List, Map, Entity, Component, Event, Behavior, Workflow, Zone, Enum
};

struct Type {
  TypeKind kind = TypeKind::Any;
  std::string name;              // for Entity/Component/Event/Behavior etc.
  std::shared_ptr<Type> elem;    // list<elem>
  std::shared_ptr<Type> key;     // map<key, val>
  std::shared_ptr<Type> val;

  bool is_numeric()  const { return kind == TypeKind::Int || kind == TypeKind::Float; }
  bool is_any()      const { return kind == TypeKind::Any; }
  bool assignable_from(const Type& other) const;

  static Type make_int()      { return {TypeKind::Int,      "int"}; }
  static Type make_float()    { return {TypeKind::Float,    "float"}; }
  static Type make_bool()     { return {TypeKind::Bool,     "bool"}; }
  static Type make_string()   { return {TypeKind::String,   "string"}; }
  static Type make_duration() { return {TypeKind::Duration, "duration"}; }
  static Type make_null()     { return {TypeKind::Null,     "null"}; }
  static Type make_any()      { return {TypeKind::Any,      "any"}; }
  static Type make_void()     { return {TypeKind::Void,     "void"}; }
  static Type make_named(TypeKind k, const std::string& n) { return {k, n}; }
  static Type from_ref(const TypeRef& tr);
};

inline bool Type::assignable_from(const Type& other) const {
  if (kind == TypeKind::Any || other.kind == TypeKind::Any) return true;
  if (kind == TypeKind::Null) return other.kind == TypeKind::Null;
  if (kind == TypeKind::Float && other.kind == TypeKind::Int) return true;
  return kind == other.kind && name == other.name;
}

inline Type Type::from_ref(const TypeRef& tr) {
  if (tr.name == "int")      return make_int();
  if (tr.name == "float")    return make_float();
  if (tr.name == "bool")     return make_bool();
  if (tr.name == "string")   return make_string();
  if (tr.name == "duration") return make_duration();
  if (tr.name == "void")     return make_void();
  if (tr.name == "any")      return make_any();
  if (tr.is_list) {
    Type t; t.kind = TypeKind::List; t.name = "list";
    if (!tr.params.empty()) t.elem = std::make_shared<Type>(from_ref(tr.params[0]));
    return t;
  }
  if (tr.is_map) {
    Type t; t.kind = TypeKind::Map; t.name = "map";
    if (tr.params.size() >= 2) {
      t.key = std::make_shared<Type>(from_ref(tr.params[0]));
      t.val = std::make_shared<Type>(from_ref(tr.params[1]));
    }
    return t;
  }
  // custom named type
  Type t; t.kind = TypeKind::Any; t.name = tr.name; return t;
}

// ═══════════════════════════════════════════════════════════════════════════
// SYMBOL TABLE
// ═══════════════════════════════════════════════════════════════════════════
struct Symbol {
  std::string name;
  Type        type;
  SrcPos      pos;
};

class Scope {
public:
  std::shared_ptr<Scope> parent;
  std::unordered_map<std::string, Symbol> symbols;

  bool define(const std::string& name, Type t, SrcPos p) {
    if (symbols.count(name)) return false;
    symbols[name] = {name, t, p};
    return true;
  }
  const Symbol* lookup(const std::string& name) const {
    auto it = symbols.find(name);
    if (it != symbols.end()) return &it->second;
    if (parent) return parent->lookup(name);
    return nullptr;
  }
};

// ═══════════════════════════════════════════════════════════════════════════
// SEMANTIC ANALYSIS RESULT
// ═══════════════════════════════════════════════════════════════════════════
struct SemaResult {
  bool ok = true;
  std::vector<Diag> diagnostics;
  std::string report() const {
    std::string s;
    for (auto& d : diagnostics) {
      s += (d.level == Diag::Error ? "ERROR" : "WARN");
      s += " line " + std::to_string(d.pos.line);
      s += ": " + d.message + "\n";
    }
    if (ok) s += "OK: semantic analysis passed\n";
    return s;
  }
};

// ═══════════════════════════════════════════════════════════════════════════
// SEMANTIC ANALYZER
// ═══════════════════════════════════════════════════════════════════════════
class Sema {
public:
  SemaResult analyze(const Program& prog);

private:
  std::vector<Diag>  diags_;
  std::shared_ptr<Scope> global_;

  // registry
  std::unordered_map<std::string, const EntityDecl*>   entities_;
  std::unordered_map<std::string, const BehaviorDecl*> behaviors_;
  std::unordered_map<std::string, const EventDecl*>    events_;
  std::unordered_map<std::string, const ComponentDecl*> components_;

  void error(SrcPos p, const std::string& msg);
  void warn(SrcPos  p, const std::string& msg);

  void collect_declarations(const Program& prog);
  void check_decl(const DeclPtr& d);
  void check_entity(const EntityDecl& d);
  void check_behavior(const BehaviorDecl& d);
  void check_workflow(const WorkflowDecl& d);
  void check_zone(const ZoneDecl& d);
  void check_scenario(const ScenarioDecl& d);
  void check_actions(const std::vector<ActionPtr>& actions, Scope& scope);
  void check_action(const Action& a, Scope& scope);
  Type check_expr(const Expr& e, Scope& scope);
  void check_transition(const TransitionDecl& td, const std::vector<StateDecl>& states);
};

} // namespace yuspec::v1
