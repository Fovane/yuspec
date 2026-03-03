#pragma once
// YUSPEC v1.0 — Lexer
#include <string>
#include <vector>
#include <unordered_map>
#include "yuspec/v1_token.h"

namespace yuspec::v1 {

// Diagnostic collected during lexing/parsing
struct Diag {
  enum Level { Error, Warning };
  Level       level = Error;
  SrcPos      pos;
  std::string message;
};

class Lexer {
public:
  explicit Lexer(std::string source, std::string filename = "<input>");

  Token next();
  const Token& peek();
  void  consume();
  bool  eof() const;

  const std::vector<Diag>& diagnostics() const { return diags_; }
  bool has_errors() const;

private:
  std::string src_;
  std::string filename_;
  int         idx_ = 0;
  SrcPos      pos_;

  bool   has_peek_ = false;
  Token  peek_tok_;

  std::vector<Diag> diags_;

  char   cur()       const;
  char   look(int n) const;
  void   advance();
  void   skip_ws_comments();

  Token  lex_number();
  Token  lex_string();
  Token  lex_word();
  Token  make(TK kind, std::string lexeme) const;
  void   error(const std::string& msg);

  static const std::unordered_map<std::string, TK>& keywords();
};

} // namespace yuspec::v1
