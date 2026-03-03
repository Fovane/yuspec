#pragma once
#include <string>
#include <cstdint>

namespace yuspec {

enum class TokenKind : uint16_t {
  // Keywords
  KW_CREATE,
  KW_TERRAIN,
  KW_HUMANOID,
  KW_PLAYER,
  KW_HOSTILE,
  KW_WITH,
  KW_HEALTH,
  KW_MANA,
  KW_COUNT,
  KW_ATTACH,
  KW_LOGIC,
  KW_TO,
  KW_MOVE,
  KW_CHASE,
  KW_CONTACT_DAMAGE,
  KW_DAMAGE,
  KW_SPAWN,
  KW_IN,
  KW_RANDOM,
  KW_AND,

  // Literals / symbols
  INT,
  X,           // 'x' in 100x100
  SEMICOLON,   // ;

  // End
  END_OF_FILE,
};

struct SourcePos {
  int line = 1;
  int col  = 1;
  int offset = 0; // byte offset in source
};

struct Token {
  TokenKind kind{};
  std::string lexeme; // exact text
  int64_t int_value = 0; // if kind==INT
  SourcePos pos{};
};

const char* to_string(TokenKind k);

} // namespace yuspec