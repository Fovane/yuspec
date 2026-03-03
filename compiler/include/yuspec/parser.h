#pragma once
#include "yuspec/lexer.h"
#include "yuspec/ast.h"

namespace yuspec {

class Parser {
public:
  explicit Parser(Lexer& lex);

  Program parse_program();

private:
  Lexer& lex_;
  Token tok_; // current token

  void advance();
  bool match(TokenKind k);
  void expect(TokenKind k, const char* what);

  Stmt parse_statement();
  Stmt parse_create();
  Stmt parse_attach();
  Stmt parse_spawn();

  int expect_int(const char* what);
};

} // namespace yuspec