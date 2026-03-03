#pragma once
// YUSPEC v1.0 — Parser
#include "yuspec/v1_lexer.h"
#include "yuspec/v1_ast.h"
#include <vector>

namespace yuspec::v1 {

class Parser {
public:
  explicit Parser(Lexer& lex);

  Program parse_program();
  const std::vector<Diag>& diagnostics() const { return diags_; }
  bool has_errors() const;

private:
  Lexer& lex_;
  Token  tok_;
  std::vector<Diag> diags_;

  // ── Token control ────────────────────────────────────────────────────────
  void    advance();
  bool    check(TK k) const;
  bool    match(TK k);
  Token   expect(TK k, const char* what);
  bool    at_eof() const;
  void    error(const std::string& msg);
  void    sync_to_decl(); // error recovery: skip to next 'define' / 'import'

  // ── Top-level ────────────────────────────────────────────────────────────
  DeclPtr parse_decl();
  DeclPtr parse_entity();
  DeclPtr parse_component();
  DeclPtr parse_behavior();
  DeclPtr parse_statemachine();
  DeclPtr parse_event_decl();
  DeclPtr parse_workflow();
  DeclPtr parse_zone();
  DeclPtr parse_system();
  DeclPtr parse_scenario();
  DeclPtr parse_import();

  // ── Members ──────────────────────────────────────────────────────────────
  PropertyDecl     parse_property();
  StateDecl        parse_state();
  TransitionDecl   parse_transition();
  RuleDecl         parse_rule();
  HandlerDecl      parse_handler();
  StepDecl         parse_step();
  SpawnAction      parse_spawn_action();
  double           parse_duration(const char* ctx);
  TypeRef          parse_type_ref();

  // ── Actions ──────────────────────────────────────────────────────────────
  std::vector<ActionPtr> parse_action_block(); // { action* }
  ActionPtr              parse_action();
  ActionPtr              parse_if_action();
  ActionPtr              parse_while_action();
  ActionPtr              parse_foreach_action();

  // ── Trigger ──────────────────────────────────────────────────────────────
  Trigger parse_trigger();

  // ── Expressions ──────────────────────────────────────────────────────────
  ExprPtr parse_expr();
  ExprPtr parse_or();
  ExprPtr parse_and();
  ExprPtr parse_equality();
  ExprPtr parse_comparison();
  ExprPtr parse_addition();
  ExprPtr parse_multiplication();
  ExprPtr parse_unary();
  ExprPtr parse_postfix();
  ExprPtr parse_primary();
};

} // namespace yuspec::v1
