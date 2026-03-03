#include "yuspec/parser.h"
#include "yuspec/diagnostic.h"

namespace yuspec {

Parser::Parser(Lexer& lex) : lex_(lex) {
  tok_ = lex_.next();
}

void Parser::advance() {
  tok_ = lex_.next();
}

bool Parser::match(TokenKind k) {
  if (tok_.kind == k) {
    advance();
    return true;
  }
  return false;
}

void Parser::expect(TokenKind k, const char* what) {
  if (tok_.kind != k) {
    Diagnostic::error_at(tok_, std::string("Expected ") + what);
  }
  advance();
}

int Parser::expect_int(const char* what) {
  if (tok_.kind != TokenKind::INT) {
    Diagnostic::error_at(tok_, std::string("Expected integer for ") + what);
  }
  int v = (int)tok_.int_value;
  advance();
  return v;
}

Program Parser::parse_program() {
  Program p;
  while (tok_.kind != TokenKind::END_OF_FILE) {
    p.statements.push_back(parse_statement());
  }
  return p;
}

Stmt Parser::parse_statement() {
  if (tok_.kind == TokenKind::KW_CREATE) return parse_create();
  if (tok_.kind == TokenKind::KW_ATTACH) return parse_attach();
  if (tok_.kind == TokenKind::KW_SPAWN)  return parse_spawn();
  Diagnostic::error_at(tok_, "Expected 'create', 'attach', or 'spawn'");
  return {}; // unreachable
}

Stmt Parser::parse_create() {
  Stmt s;
  expect(TokenKind::KW_CREATE, "'create'");

  if (match(TokenKind::KW_TERRAIN)) {
    s.kind = StmtKind::CreateTerrain;
    s.w = expect_int("terrain width");
    expect(TokenKind::X, "'x' in WxH");
    s.h = expect_int("terrain height");
    expect(TokenKind::SEMICOLON, "';'");
    return s;
  }

  expect(TokenKind::KW_HUMANOID, "'humanoid'");

  if (match(TokenKind::KW_PLAYER)) {
    s.kind = StmtKind::CreatePlayer;
    expect(TokenKind::KW_WITH, "'with'");
    expect(TokenKind::KW_HEALTH, "'health'");
    s.playerHealth = expect_int("player health");
    expect(TokenKind::KW_AND, "'and'");
    expect(TokenKind::KW_MANA, "'mana'");
    s.playerMana = expect_int("player mana");
    expect(TokenKind::SEMICOLON, "';'");
    return s;
  }

  if (match(TokenKind::KW_HOSTILE)) {
    s.kind = StmtKind::CreateHostiles;
    expect(TokenKind::KW_COUNT, "'count'");
    s.hostileCount = expect_int("hostile count");
    expect(TokenKind::KW_WITH, "'with'");
    expect(TokenKind::KW_HEALTH, "'health'");
    s.hostileHealth = expect_int("hostile health");
    expect(TokenKind::SEMICOLON, "';'");
    return s;
  }

  Diagnostic::error_at(tok_, "Expected 'player' or 'hostile' after 'humanoid'");
  return {}; // unreachable
}

Stmt Parser::parse_attach() {
  Stmt s;
  s.kind = StmtKind::AttachLogic;

  expect(TokenKind::KW_ATTACH, "'attach'");

  // logic name
  if (match(TokenKind::KW_MOVE)) {
    s.logic.name = "move";
  } else if (match(TokenKind::KW_CHASE)) {
    s.logic.name = "chase";
  } else if (match(TokenKind::KW_CONTACT_DAMAGE)) {
    s.logic.name = "contact_damage";
  } else {
    Diagnostic::error_at(tok_, "Expected logic name: move/chase/contact_damage");
  }

  expect(TokenKind::KW_LOGIC, "'logic'");
  expect(TokenKind::KW_TO, "'to'");

  // target
  if (match(TokenKind::KW_PLAYER)) {
    s.target = TargetRef{TargetKind::Id, "player"};
  } else if (match(TokenKind::KW_HOSTILE)) {
    s.target = TargetRef{TargetKind::Tag, "hostile"};
  } else {
    Diagnostic::error_at(tok_, "Expected target: player/hostile");
  }

  // extra params for contact_damage
  if (s.logic.name == "contact_damage") {
    expect(TokenKind::KW_WITH, "'with'");
    expect(TokenKind::KW_DAMAGE, "'damage'");
    int dmg = expect_int("damage");
    s.logic.params["damage"] = dmg;
  }

  expect(TokenKind::SEMICOLON, "';'");
  return s;
}

Stmt Parser::parse_spawn() {
  Stmt s;
  s.kind = StmtKind::Spawn;

  expect(TokenKind::KW_SPAWN, "'spawn'");
  expect(TokenKind::KW_HOSTILE, "'hostile'");
  expect(TokenKind::KW_IN, "'in'");
  expect(TokenKind::KW_TERRAIN, "'terrain'");
  expect(TokenKind::KW_RANDOM, "'random'");
  expect(TokenKind::SEMICOLON, "';'");
  s.spawnTarget = TargetRef{TargetKind::Tag, "hostile"};
  return s;
}

} // namespace yuspec