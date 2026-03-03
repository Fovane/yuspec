// YUSPEC v1.0 — Lexer Implementation
#include "yuspec/v1_lexer.h"
#include <cctype>
#include <stdexcept>
#include <sstream>
#include <algorithm>

namespace yuspec::v1 {

Lexer::Lexer(std::string source, std::string filename)
  : src_(std::move(source)), filename_(std::move(filename)) {
  pos_.line = 1; pos_.col = 1; pos_.offset = 0;
}

char Lexer::cur() const {
  return (idx_ < (int)src_.size()) ? src_[idx_] : '\0';
}
char Lexer::look(int n) const {
  int i = idx_ + n;
  return (i >= 0 && i < (int)src_.size()) ? src_[i] : '\0';
}
bool Lexer::eof() const { return idx_ >= (int)src_.size(); }

void Lexer::advance() {
  if (eof()) return;
  char c = src_[idx_++];
  pos_.offset++;
  if (c == '\n') { pos_.line++; pos_.col = 1; }
  else           { pos_.col++; }
}

void Lexer::error(const std::string& msg) {
  diags_.push_back({ Diag::Error, pos_, msg });
}

bool Lexer::has_errors() const {
  return std::any_of(diags_.begin(), diags_.end(),
    [](const Diag& d){ return d.level == Diag::Error; });
}

Token Lexer::make(TK kind, std::string lexeme) const {
  Token t;
  t.kind = kind;
  t.lexeme = std::move(lexeme);
  t.pos = pos_;
  return t;
}

void Lexer::skip_ws_comments() {
  while (!eof()) {
    char c = cur();
    // whitespace
    if (c == ' ' || c == '\t' || c == '\r' || c == '\n') { advance(); continue; }
    // line comment
    if (c == '/' && look(1) == '/') { while (!eof() && cur() != '\n') advance(); continue; }
    // block comment
    if (c == '/' && look(1) == '*') {
      advance(); advance(); // consume /*
      while (!eof()) {
        if (cur() == '*' && look(1) == '/') { advance(); advance(); break; }
        advance();
      }
      continue;
    }
    break;
  }
}

// ─── Number literal: int, float, duration ────────────────────────────────
Token Lexer::lex_number() {
  SrcPos at = pos_;
  std::string s;
  bool is_float = false;

  while (!eof() && std::isdigit((unsigned char)cur())) { s += cur(); advance(); }
  if (!eof() && cur() == '.' && std::isdigit((unsigned char)look(1))) {
    is_float = true;
    s += cur(); advance();
    while (!eof() && std::isdigit((unsigned char)cur())) { s += cur(); advance(); }
  }

  // Duration suffix: ms, s, m, h, d
  std::string suffix;
  if (!eof()) {
    if (cur() == 'm' && look(1) == 's') { suffix = "ms"; advance(); advance(); }
    else if (cur() == 's' && (eof()||!std::isalnum((unsigned char)look(1)))) { suffix = "s"; advance(); }
    else if (cur() == 'm' && (eof()||!std::isalnum((unsigned char)look(1)))) { suffix = "m"; advance(); }
    else if (cur() == 'h' && (eof()||!std::isalnum((unsigned char)look(1)))) { suffix = "h"; advance(); }
    else if (cur() == 'd' && (eof()||!std::isalnum((unsigned char)look(1)))) { suffix = "d"; advance(); }
  }

  Token t;
  t.pos = at;
  t.lexeme = s + suffix;

  if (!suffix.empty()) {
    t.kind = TK::LIT_DURATION;
    double v = is_float ? std::stod(s) : (double)std::stoll(s);
    if      (suffix == "ms") t.dur_ms = v;
    else if (suffix == "s")  t.dur_ms = v * 1000.0;
    else if (suffix == "m")  t.dur_ms = v * 60000.0;
    else if (suffix == "h")  t.dur_ms = v * 3600000.0;
    else if (suffix == "d")  t.dur_ms = v * 86400000.0;
  } else if (is_float) {
    t.kind = TK::LIT_FLOAT;
    t.flt_val = std::stod(s);
  } else {
    t.kind = TK::LIT_INT;
    t.int_val = std::stoll(s);
  }
  return t;
}

// ─── String literal  "..." with escape sequences ─────────────────────────
Token Lexer::lex_string() {
  SrcPos at = pos_;
  advance(); // consume opening "
  std::string val;
  while (!eof() && cur() != '"') {
    if (cur() == '\\') {
      advance();
      switch (cur()) {
        case 'n':  val += '\n'; break;
        case 't':  val += '\t'; break;
        case 'r':  val += '\r'; break;
        case '"':  val += '"';  break;
        case '\\': val += '\\'; break;
        default:   val += cur(); break;
      }
      advance();
    } else {
      val += cur(); advance();
    }
  }
  if (eof()) { error("Unterminated string literal"); }
  else       { advance(); } // consume closing "

  Token t;
  t.kind = TK::LIT_STRING;
  // Store the unquoted string value directly in lexeme for parser convenience
  t.lexeme = val;
  t.pos = at;
  return t;
}

// ─── Identifier or keyword ───────────────────────────────────────────────
Token Lexer::lex_word() {
  SrcPos at = pos_;
  std::string s;
  while (!eof() && (std::isalnum((unsigned char)cur()) || cur() == '_')) {
    s += cur(); advance();
  }

  // Special case: "statemachine" can appear as two words "state machine" — we handle
  // this by checking if we just scanned "state" and next non-ws is "machine"
  // (handled in parser level; lexer emits KW_STATE)

  auto& kw = keywords();
  auto it = kw.find(s);
  Token t;
  t.pos = at;
  t.lexeme = s;
  if (it != kw.end()) {
    t.kind = it->second;
    if (t.kind == TK::LIT_BOOL_TRUE)  t.int_val = 1;
    if (t.kind == TK::LIT_BOOL_FALSE) t.int_val = 0;
  } else {
    t.kind = TK::IDENT;
  }
  return t;
}

// ─── Keyword table ───────────────────────────────────────────────────────
const std::unordered_map<std::string, TK>& Lexer::keywords() {
  static const std::unordered_map<std::string, TK> k = {
    // Declarations
    {"define",       TK::KW_DEFINE},
    {"entity",       TK::KW_ENTITY},
    {"component",    TK::KW_COMPONENT},
    {"behavior",     TK::KW_BEHAVIOR},
    {"for",          TK::KW_FOR},
    {"statemachine", TK::KW_STATEMACHINE},
    {"event",        TK::KW_EVENT},
    {"workflow",     TK::KW_WORKFLOW},
    {"step",         TK::KW_STEP},
    {"zone",         TK::KW_ZONE},
    {"system",       TK::KW_SYSTEM},
    {"scenario",     TK::KW_SCENARIO},
    {"import",       TK::KW_IMPORT},
    {"rule",         TK::KW_RULE},
    // Properties
    {"property",     TK::KW_PROPERTY},
    {"has",          TK::KW_HAS},
    {"default",      TK::KW_DEFAULT},
    {"actor",        TK::KW_ACTOR},
    // State machine
    {"state",        TK::KW_STATE},
    {"initial",      TK::KW_INITIAL},
    {"terminal",     TK::KW_TERMINAL},
    {"timeout",      TK::KW_TIMEOUT},
    {"retry",        TK::KW_RETRY},
    {"on",           TK::KW_ON},
    {"from",         TK::KW_FROM},
    {"when",         TK::KW_WHEN},
    {"then",         TK::KW_THEN},
    {"transition",   TK::KW_TRANSITION},
    // Actions
    {"spawn",        TK::KW_SPAWN},
    {"emit",         TK::KW_EMIT},
    {"set",          TK::KW_SET},
    {"call",         TK::KW_CALL},
    {"create",       TK::KW_CREATE},
    {"destroy",      TK::KW_DESTROY},
    {"attach",       TK::KW_ATTACH},
    {"detach",       TK::KW_DETACH},
    {"log",          TK::KW_LOG},
    {"assert",       TK::KW_ASSERT},
    {"wait",         TK::KW_WAIT},
    {"fail",         TK::KW_FAIL},
    {"expect",       TK::KW_EXPECT},
    {"within",       TK::KW_WITHIN},
    // Control flow
    {"if",           TK::KW_IF},
    {"else",         TK::KW_ELSE},
    {"while",        TK::KW_WHILE},
    {"foreach",      TK::KW_FOR_EACH},
    {"in",           TK::KW_IN},
    {"break",        TK::KW_BREAK},
    {"continue",     TK::KW_CONTINUE},
    {"return",       TK::KW_RETURN},
    {"let",          TK::KW_LET},
    // Types
    {"int",          TK::KW_INT_TYPE},
    {"float",        TK::KW_FLOAT_TYPE},
    {"bool",         TK::KW_BOOL_TYPE},
    {"string",       TK::KW_STRING_TYPE},
    {"duration",     TK::KW_DURATION_TYPE},
    {"list",         TK::KW_LIST},
    {"map",          TK::KW_MAP},
    {"any",          TK::KW_ANY},
    {"void",         TK::KW_VOID},
    // Literals
    {"true",         TK::LIT_BOOL_TRUE},
    {"false",        TK::LIT_BOOL_FALSE},
    {"null",         TK::LIT_NULL},
  };
  return k;
}

// ─── Main scan function ───────────────────────────────────────────────────
Token Lexer::next() {
  skip_ws_comments();
  if (eof()) { Token t; t.kind = TK::END_OF_FILE; t.pos = pos_; return t; }

  SrcPos at = pos_;
  char c = cur();

  // Numbers
  if (std::isdigit((unsigned char)c)) return lex_number();
  // String
  if (c == '"') return lex_string();
  // Word / keyword
  if (std::isalpha((unsigned char)c) || c == '_') return lex_word();

  // Two-char operators
  char n = look(1);
  auto two = [&](TK k, const char* lx) -> Token {
    advance(); advance();
    Token t; t.kind = k; t.lexeme = lx; t.pos = at; return t;
  };
  auto one = [&](TK k, const char* lx) -> Token {
    advance();
    Token t; t.kind = k; t.lexeme = lx; t.pos = at; return t;
  };

  if (c == '-' && n == '>') return two(TK::ARROW,       "->");
  if (c == '=' && n == '=') return two(TK::EQ,          "==");
  if (c == '!' && n == '=') return two(TK::NEQ,         "!=");
  if (c == '<' && n == '=') return two(TK::LTE,         "<=");
  if (c == '>' && n == '=') return two(TK::GTE,         ">=");
  if (c == '&' && n == '&') return two(TK::AND_AND,     "&&");
  if (c == '|' && n == '|') return two(TK::OR_OR,       "||");
  if (c == '+' && n == '=') return two(TK::PLUS_ASSIGN, "+=");
  if (c == '-' && n == '=') return two(TK::MINUS_ASSIGN,"-=");

  // Single-char operators & punctuation
  switch (c) {
    case '+': return one(TK::PLUS,     "+");
    case '-': return one(TK::MINUS,    "-");
    case '*': return one(TK::STAR,     "*");
    case '/': return one(TK::SLASH,    "/");
    case '%': return one(TK::PERCENT,  "%");
    case '<': return one(TK::LT,       "<");
    case '>': return one(TK::GT,       ">");
    case '!': return one(TK::BANG,     "!");
    case '.': return one(TK::DOT,      ".");
    case '=': return one(TK::ASSIGN,   "=");
    case ':': return one(TK::COLON,    ":");
    case '@': return one(TK::AT,       "@");
    case ';': return one(TK::SEMICOLON,";");
    case ',': return one(TK::COMMA,    ",");
    case '{': return one(TK::LBRACE,   "{");
    case '}': return one(TK::RBRACE,   "}");
    case '(': return one(TK::LPAREN,   "(");
    case ')': return one(TK::RPAREN,   ")");
    case '[': return one(TK::LBRACKET, "[");
    case ']': return one(TK::RBRACKET, "]");
    default: break;
  }

  // Unknown
  std::string unk(1, c);
  error("Unexpected character: '" + unk + "'");
  advance();
  Token t; t.kind = TK::UNKNOWN; t.lexeme = unk; t.pos = at;
  return t;
}

const Token& Lexer::peek() {
  if (!has_peek_) { peek_tok_ = next(); has_peek_ = true; }
  return peek_tok_;
}
void Lexer::consume() { (void)peek(); has_peek_ = false; }

const char* tk_name(TK k) {
  switch (k) {
    case TK::KW_DEFINE:       return "define";
    case TK::KW_ENTITY:       return "entity";
    case TK::KW_COMPONENT:    return "component";
    case TK::KW_BEHAVIOR:     return "behavior";
    case TK::KW_FOR:          return "for";
    case TK::KW_STATEMACHINE: return "statemachine";
    case TK::KW_EVENT:        return "event";
    case TK::KW_WORKFLOW:     return "workflow";
    case TK::KW_STEP:         return "step";
    case TK::KW_ZONE:         return "zone";
    case TK::KW_SYSTEM:       return "system";
    case TK::KW_SCENARIO:     return "scenario";
    case TK::KW_IMPORT:       return "import";
    case TK::KW_RULE:         return "rule";
    case TK::KW_PROPERTY:     return "property";
    case TK::KW_HAS:          return "has";
    case TK::KW_DEFAULT:      return "default";
    case TK::KW_ACTOR:        return "actor";
    case TK::KW_STATE:        return "state";
    case TK::KW_INITIAL:      return "initial";
    case TK::KW_TERMINAL:     return "terminal";
    case TK::KW_TIMEOUT:      return "timeout";
    case TK::KW_RETRY:        return "retry";
    case TK::KW_ON:           return "on";
    case TK::KW_FROM:         return "from";
    case TK::KW_WHEN:         return "when";
    case TK::KW_THEN:         return "then";
    case TK::KW_TRANSITION:   return "transition";
    case TK::KW_SPAWN:        return "spawn";
    case TK::KW_EMIT:         return "emit";
    case TK::KW_SET:          return "set";
    case TK::KW_CALL:         return "call";
    case TK::KW_CREATE:       return "create";
    case TK::KW_DESTROY:      return "destroy";
    case TK::KW_ATTACH:       return "attach";
    case TK::KW_DETACH:       return "detach";
    case TK::KW_LOG:          return "log";
    case TK::KW_ASSERT:       return "assert";
    case TK::KW_WAIT:         return "wait";
    case TK::KW_FAIL:         return "fail";
    case TK::KW_EXPECT:       return "expect";
    case TK::KW_WITHIN:       return "within";
    case TK::KW_IF:           return "if";
    case TK::KW_ELSE:         return "else";
    case TK::KW_WHILE:        return "while";
    case TK::KW_FOR_EACH:     return "foreach";
    case TK::KW_IN:           return "in";
    case TK::KW_BREAK:        return "break";
    case TK::KW_CONTINUE:     return "continue";
    case TK::KW_RETURN:       return "return";
    case TK::KW_LET:          return "let";
    case TK::KW_INT_TYPE:     return "int";
    case TK::KW_FLOAT_TYPE:   return "float";
    case TK::KW_BOOL_TYPE:    return "bool";
    case TK::KW_STRING_TYPE:  return "string";
    case TK::KW_DURATION_TYPE:return "duration";
    case TK::KW_LIST:         return "list";
    case TK::KW_MAP:          return "map";
    case TK::KW_ANY:          return "any";
    case TK::KW_VOID:         return "void";
    case TK::LIT_INT:         return "INT";
    case TK::LIT_FLOAT:       return "FLOAT";
    case TK::LIT_STRING:      return "STRING";
    case TK::LIT_BOOL_TRUE:   return "true";
    case TK::LIT_BOOL_FALSE:  return "false";
    case TK::LIT_DURATION:    return "DURATION";
    case TK::LIT_NULL:        return "null";
    case TK::IDENT:           return "IDENT";
    case TK::PLUS:            return "+";
    case TK::MINUS:           return "-";
    case TK::STAR:            return "*";
    case TK::SLASH:           return "/";
    case TK::PERCENT:         return "%";
    case TK::EQ:              return "==";
    case TK::NEQ:             return "!=";
    case TK::LT:              return "<";
    case TK::GT:              return ">";
    case TK::LTE:             return "<=";
    case TK::GTE:             return ">=";
    case TK::AND_AND:         return "&&";
    case TK::OR_OR:           return "||";
    case TK::BANG:            return "!";
    case TK::ARROW:           return "->";
    case TK::DOT:             return ".";
    case TK::ASSIGN:          return "=";
    case TK::PLUS_ASSIGN:     return "+=";
    case TK::MINUS_ASSIGN:    return "-=";
    case TK::COLON:           return ":";
    case TK::AT:              return "@";
    case TK::SEMICOLON:       return ";";
    case TK::COMMA:           return ",";
    case TK::LBRACE:          return "{";
    case TK::RBRACE:          return "}";
    case TK::LPAREN:          return "(";
    case TK::RPAREN:          return ")";
    case TK::LBRACKET:        return "[";
    case TK::RBRACKET:        return "]";
    case TK::X_DIM:           return "x";
    case TK::END_OF_FILE:     return "EOF";
    default:                  return "UNKNOWN";
  }
}

} // namespace yuspec::v1
