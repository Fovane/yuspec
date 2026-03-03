#pragma once
#include <string>
#include <unordered_map>
#include "yuspec/token.h"

namespace yuspec {

class Lexer {
public:
  explicit Lexer(std::string source);

  Token next();
  const Token& peek(); // lookahead 1
  void consume();      // consume peeked token

private:
  std::string src_;
  int idx_ = 0;
  SourcePos pos_{};

  bool has_peek_ = false;
  Token peek_tok_{};

  char cur() const;
  char next_char() const;
  bool eof() const;

  void advance();
  void skip_ws_and_comments();

  Token lex_int();
  Token lex_word_or_keyword();
  Token make(TokenKind kind, const std::string& lexeme, SourcePos at);

  static const std::unordered_map<std::string, TokenKind>& keywords();
};

} // namespace yuspec