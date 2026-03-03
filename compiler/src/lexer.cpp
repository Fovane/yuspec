#include "yuspec/lexer.h"
#include "yuspec/diagnostic.h"
#include <cctype>

namespace yuspec {

Lexer::Lexer(std::string source) : src_(std::move(source)) {}

char Lexer::cur() const { return (idx_ >= 0 && idx_ < (int)src_.size()) ? src_[idx_] : '\0'; }
char Lexer::next_char() const { return (idx_ + 1 < (int)src_.size()) ? src_[idx_ + 1] : '\0'; }
bool Lexer::eof() const { return idx_ >= (int)src_.size(); }

void Lexer::advance() {
  if (eof()) return;
  char c = src_[idx_++];
  pos_.offset++;

  if (c == '\n') {
    pos_.line++;
    pos_.col = 1;
  } else {
    pos_.col++;
  }
}

Token Lexer::make(TokenKind kind, const std::string& lexeme, SourcePos at) {
  Token t;
  t.kind = kind;
  t.lexeme = lexeme;
  t.pos = at;
  return t;
}

void Lexer::skip_ws_and_comments() {
  while (!eof()) {
    char c = cur();

    // whitespace
    if (c == ' ' || c == '\t' || c == '\r' || c == '\n') {
      advance();
      continue;
    }

    // comment // ...
    if (c == '/' && next_char() == '/') {
      while (!eof() && cur() != '\n') advance();
      continue;
    }

    break;
  }
}

Token Lexer::lex_int() {
  SourcePos at = pos_;
  std::string s;
  while (!eof() && std::isdigit((unsigned char)cur())) {
    s.push_back(cur());
    advance();
  }

  Token t = make(TokenKind::INT, s, at);
  // safe parse (v0.1 expects positive ints)
  try {
    t.int_value = std::stoll(s);
  } catch (...) {
    Diagnostic::error_at(t, "Invalid integer literal");
  }
  return t;
}

Token Lexer::lex_word_or_keyword() {
  SourcePos at = pos_;
  std::string s;
  while (!eof()) {
    char c = cur();
    if (std::isalnum((unsigned char)c) || c == '_' ) {
      s.push_back(c);
      advance();
    } else {
      break;
    }
  }

  auto& kw = keywords();
  auto it = kw.find(s);
  if (it != kw.end()) {
    return make(it->second, s, at);
  }

  // v0.1: unknown words are hard errors
  Token t = make(TokenKind::END_OF_FILE, s, at);
  Diagnostic::error_at(t, "Unknown keyword");
  return t; // unreachable
}

const std::unordered_map<std::string, TokenKind>& Lexer::keywords() {
  static const std::unordered_map<std::string, TokenKind> k = {
    {"create", TokenKind::KW_CREATE},
    {"terrain", TokenKind::KW_TERRAIN},
    {"humanoid", TokenKind::KW_HUMANOID},
    {"player", TokenKind::KW_PLAYER},
    {"hostile", TokenKind::KW_HOSTILE},
    {"with", TokenKind::KW_WITH},
    {"health", TokenKind::KW_HEALTH},
    {"mana", TokenKind::KW_MANA},
    {"count", TokenKind::KW_COUNT},
    {"attach", TokenKind::KW_ATTACH},
    {"logic", TokenKind::KW_LOGIC},
    {"to", TokenKind::KW_TO},
    {"move", TokenKind::KW_MOVE},
    {"chase", TokenKind::KW_CHASE},
    {"contact_damage", TokenKind::KW_CONTACT_DAMAGE},
    {"damage", TokenKind::KW_DAMAGE},
    {"spawn", TokenKind::KW_SPAWN},
    {"in", TokenKind::KW_IN},
    {"random", TokenKind::KW_RANDOM},
    {"and", TokenKind::KW_AND},
  };
  return k;
}

Token Lexer::next() {
  skip_ws_and_comments();

  if (eof()) {
    return make(TokenKind::END_OF_FILE, "", pos_);
  }

  SourcePos at = pos_;
  char c = cur();

  // int
  if (std::isdigit((unsigned char)c)) return lex_int();

  // symbols (MUST come before word/keyword)
  if (c == 'x') { advance(); return make(TokenKind::X, "x", at); }
  if (c == ';') { advance(); return make(TokenKind::SEMICOLON, ";", at); }

  // word / keyword
  if (std::isalpha((unsigned char)c) || c == '_') return lex_word_or_keyword();

  // Unknown char
  Token t = make(TokenKind::END_OF_FILE, std::string(1, c), at);
  Diagnostic::error_at(t, "Unexpected character");
  return t; // unreachable
}

const Token& Lexer::peek() {
  if (!has_peek_) {
    peek_tok_ = next();
    has_peek_ = true;
  }
  return peek_tok_;
}

void Lexer::consume() {
  (void)peek();
  has_peek_ = false;
}

} // namespace yuspec