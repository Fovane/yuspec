#pragma once
// YUSPEC v1.0 — Abstract Syntax Tree
// Entity-Behavior Programming (EBP)
#include <string>
#include <vector>
#include <memory>
#include <unordered_map>
#include <variant>
#include <optional>
#include "yuspec/v1_token.h"

namespace yuspec::v1 {

// ─── Forward declarations ─────────────────────────────────────────────────
struct Expr;
struct Stmt;
struct Action;
struct Decl;
using ExprPtr   = std::shared_ptr<Expr>;
using StmtPtr   = std::shared_ptr<Stmt>;
using ActionPtr = std::shared_ptr<Action>;
using DeclPtr   = std::shared_ptr<Decl>;

// ═══════════════════════════════════════════════════════════════════════════
// TYPE REFERENCES
// ═══════════════════════════════════════════════════════════════════════════
enum class BuiltinType { Int, Float, Bool, String, Duration, Any, Void };

struct TypeRef {
  std::string name;      // "int"/"float"/"bool"/"string"/"duration" or custom name
  std::vector<TypeRef> params; // for list<T>, map<K,V>
  bool is_list = false;
  bool is_map  = false;
  SrcPos pos;
};

// ═══════════════════════════════════════════════════════════════════════════
// EXPRESSIONS
// ═══════════════════════════════════════════════════════════════════════════
struct LiteralExpr {
  enum class Kind { Int, Float, Bool, String, Duration, Null } kind;
  int64_t int_val = 0;
  double  flt_val = 0.0;
  bool    bool_val = false;
  std::string str_val;
  double  dur_ms  = 0.0; // for duration
};

struct IdentExpr    { std::string name; };
struct MemberExpr   { ExprPtr obj; std::string field; };
struct IndexExpr    { ExprPtr obj; ExprPtr index; };
struct CallExpr     { ExprPtr callee; std::vector<ExprPtr> args; };
struct BinaryExpr   { std::string op; ExprPtr left, right; };
struct UnaryExpr    { std::string op; ExprPtr operand; };
struct ListExpr     { std::vector<ExprPtr> elements; };
struct MapExpr      { std::vector<std::pair<ExprPtr,ExprPtr>> entries; };

struct Expr {
  SrcPos pos;
  using Data = std::variant<
    LiteralExpr, IdentExpr, MemberExpr, IndexExpr,
    CallExpr, BinaryExpr, UnaryExpr, ListExpr, MapExpr
  >;
  Data data;

  template<typename T> bool is() const { return std::holds_alternative<T>(data); }
  template<typename T> const T& as() const { return std::get<T>(data); }
  template<typename T> T& as() { return std::get<T>(data); }
};

// ═══════════════════════════════════════════════════════════════════════════
// PROPERTY DECLARATION  (used in entities, components, events, behaviors)
// ═══════════════════════════════════════════════════════════════════════════
struct PropertyDecl {
  std::string name;
  TypeRef     type;
  std::optional<ExprPtr> default_val;
  SrcPos      pos;
};

// ═══════════════════════════════════════════════════════════════════════════
// ACTIONS  (imperative steps inside behaviors / scenarios)
// ═══════════════════════════════════════════════════════════════════════════

struct SpawnAction {
  TypeRef               entity_type;
  std::optional<ExprPtr> count;
  std::optional<ExprPtr> placement;   // e.g. "random" or position expr
  std::optional<ExprPtr> at_expr;
  std::vector<std::pair<std::string, ExprPtr>> with_props;
};

struct EmitAction {
  std::string event_name;
  std::vector<std::pair<std::string, ExprPtr>> fields;
  std::optional<ExprPtr> target;
};

struct SetAction    { ExprPtr lhs; ExprPtr rhs; std::string op; }; // =, +=, -=
struct CallAction   { ExprPtr callee; std::vector<ExprPtr> args; };
struct DestroyAction{ ExprPtr target; };
struct AttachAction { std::string behavior; ExprPtr target; };
struct DetachAction { std::string behavior; ExprPtr target; };
struct LogAction    { ExprPtr message; };
struct AssertAction { ExprPtr condition; std::optional<std::string> message; };
struct LetAction    { std::string name; ExprPtr init; };   // local variable binding
struct WaitAction   { double dur_ms; };
struct TransitionAction { std::string target_state; };
struct RetryAction  {};
struct FailAction   { std::optional<ExprPtr> message; };
struct BreakAction  {};
struct ContinueAction {};
struct ReturnAction { std::optional<ExprPtr> value; };
struct ExpectAction { ExprPtr condition; std::optional<double> within_ms; };

struct IfAction {
  ExprPtr condition;
  std::vector<ActionPtr> then_body;
  std::vector<ActionPtr> else_body;
};
struct WhileAction {
  ExprPtr condition;
  std::vector<ActionPtr> body;
};
struct ForeachAction {
  std::string var;
  ExprPtr     collection;
  std::vector<ActionPtr> body;
};

struct Action {
  SrcPos pos;
  using Data = std::variant<
    SpawnAction, EmitAction, SetAction, LetAction, CallAction, DestroyAction,
    AttachAction, DetachAction, LogAction, AssertAction, WaitAction,
    TransitionAction, RetryAction, FailAction, BreakAction, ContinueAction,
    ReturnAction, ExpectAction, IfAction, WhileAction, ForeachAction
  >;
  Data data;
  template<typename T> bool is() const { return std::holds_alternative<T>(data); }
  template<typename T> const T& as() const { return std::get<T>(data); }
};

// ═══════════════════════════════════════════════════════════════════════════
// TRIGGER  (what fires a state transition / rule / event handler)
// ═══════════════════════════════════════════════════════════════════════════
enum class TriggerKind { Event, Timeout, Condition };

struct Trigger {
  TriggerKind kind;
  std::string event_name; // TriggerKind::Event
  ExprPtr     condition;  // TriggerKind::Condition
  SrcPos pos;
};

// ═══════════════════════════════════════════════════════════════════════════
// STATE DECLARATION
// ═══════════════════════════════════════════════════════════════════════════
struct StateDecl {
  std::string name;
  bool is_initial  = false;
  bool is_terminal = false;
  std::optional<double> timeout_ms;
  int retry_count  = 0;
  std::vector<ActionPtr> on_enter;
  std::vector<ActionPtr> on_exit;
  SrcPos pos;
};

// ═══════════════════════════════════════════════════════════════════════════
// TRANSITION (on <trigger> [from <state>] -> <state> { actions })
// ═══════════════════════════════════════════════════════════════════════════
struct TransitionDecl {
  Trigger                from_trigger;
  std::optional<std::string> from_state; // if absent, applies from any state
  std::string            to_state;
  std::vector<ActionPtr> actions;
  SrcPos pos;
};

// ═══════════════════════════════════════════════════════════════════════════
// RULE  (when <condition> then { actions })
// ═══════════════════════════════════════════════════════════════════════════
struct RuleDecl {
  std::optional<std::string> name;
  ExprPtr                    condition;
  std::vector<ActionPtr>     actions;
  SrcPos pos;
};

// ═══════════════════════════════════════════════════════════════════════════
// EVENT HANDLER
// ═══════════════════════════════════════════════════════════════════════════
struct HandlerDecl {
  Trigger                trigger;
  std::vector<ActionPtr> actions;
  SrcPos pos;
};

// ═══════════════════════════════════════════════════════════════════════════
// DECLARATIONS
// ═══════════════════════════════════════════════════════════════════════════

struct EntityDecl {
  std::string                   name;
  std::vector<PropertyDecl>     properties;
  std::vector<std::string>      has_behaviors; // 'has BehaviorName'
  std::vector<std::string>      has_components;
  SrcPos pos;
};

struct ComponentDecl {
  std::string               name;
  std::vector<PropertyDecl> properties;
  SrcPos pos;
};

struct BehaviorDecl {
  std::string                for_type; // entity type it targets (optional)
  std::string                name;
  std::vector<PropertyDecl>  properties;
  std::vector<StateDecl>     states;
  std::vector<TransitionDecl> transitions;
  std::vector<RuleDecl>      rules;
  std::vector<HandlerDecl>   handlers;
  SrcPos pos;
};

struct StateMachineDecl {
  std::string                 name;
  std::vector<StateDecl>      states;
  std::vector<TransitionDecl> transitions;
  SrcPos pos;
};

struct EventDecl {
  std::string               name;
  std::vector<PropertyDecl> fields;
  SrcPos pos;
};

struct StepDecl {
  std::string                    name;
  std::optional<std::string>     actor_type;
  std::optional<double>          timeout_ms;
  int                            retry_count = 0;
  std::vector<ActionPtr>         on_enter;
  SrcPos pos;
};

struct WorkflowDecl {
  std::string                name;
  std::vector<StepDecl>      steps;
  std::vector<RuleDecl>      rules;
  std::vector<HandlerDecl>   handlers;
  std::vector<PropertyDecl>  properties;
  SrcPos pos;
};

struct ZoneDecl {
  std::string name;
  int terrain_w = 0, terrain_h = 0;
  bool has_terrain = false;
  std::vector<SpawnAction>   spawns;
  std::vector<RuleDecl>      rules;
  std::vector<PropertyDecl>  properties;
  SrcPos pos;
};

struct SystemDecl {
  std::string              name;
  std::optional<double>    tick_ms;
  std::vector<HandlerDecl> handlers;
  std::vector<RuleDecl>    rules;
  SrcPos pos;
};

struct ScenarioDecl {
  std::string              name;
  std::vector<ActionPtr>   steps;
  SrcPos pos;
};

struct ImportDecl {
  std::string path;
  SrcPos pos;
};

struct Decl {
  SrcPos pos;
  using Data = std::variant<
    EntityDecl, ComponentDecl, BehaviorDecl, StateMachineDecl,
    EventDecl, WorkflowDecl, ZoneDecl, SystemDecl, ScenarioDecl, ImportDecl
  >;
  Data data;
  template<typename T> bool is() const { return std::holds_alternative<T>(data); }
  template<typename T> const T& as() const { return std::get<T>(data); }
  template<typename T> T& as() { return std::get<T>(data); }
};

// ═══════════════════════════════════════════════════════════════════════════
// PROGRAM
// ═══════════════════════════════════════════════════════════════════════════
struct Program {
  std::vector<DeclPtr> declarations;
  std::string source_file;
};

} // namespace yuspec::v1
