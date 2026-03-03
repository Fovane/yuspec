#include "yuspec/diagnostic.h"
#include <sstream>

namespace yuspec {

void Diagnostic::error_at(const Token& tok, const std::string& message) {
  std::ostringstream oss;
  oss << "Yuspec error at line " << tok.pos.line << ", col " << tok.pos.col
      << ": " << message;
  if (!tok.lexeme.empty()) {
    oss << " (got '" << tok.lexeme << "')";
  }
  throw CompileError(oss.str());
}

const char* to_string(TokenKind k) {
  switch (k) {
    case TokenKind::KW_CREATE: return "create";
    case TokenKind::KW_TERRAIN: return "terrain";
    case TokenKind::KW_HUMANOID: return "humanoid";
    case TokenKind::KW_PLAYER: return "player";
    case TokenKind::KW_HOSTILE: return "hostile";
    case TokenKind::KW_WITH: return "with";
    case TokenKind::KW_HEALTH: return "health";
    case TokenKind::KW_MANA: return "mana";
    case TokenKind::KW_COUNT: return "count";
    case TokenKind::KW_ATTACH: return "attach";
    case TokenKind::KW_LOGIC: return "logic";
    case TokenKind::KW_TO: return "to";
    case TokenKind::KW_MOVE: return "move";
    case TokenKind::KW_CHASE: return "chase";
    case TokenKind::KW_CONTACT_DAMAGE: return "contact_damage";
    case TokenKind::KW_DAMAGE: return "damage";
    case TokenKind::KW_SPAWN: return "spawn";
    case TokenKind::KW_IN: return "in";
    case TokenKind::KW_RANDOM: return "random";
    case TokenKind::KW_AND: return "and";
    case TokenKind::INT: return "INT";
    case TokenKind::X: return "x";
    case TokenKind::SEMICOLON: return ";";
    case TokenKind::END_OF_FILE: return "EOF";
  }
  return "UNKNOWN";
}

} // namespace yuspec