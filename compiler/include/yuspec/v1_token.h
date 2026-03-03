#pragma once
// YUSPEC v1.0 — Token definitions
// Entity-Behavior Programming (EBP): the third paradigm.
#include <string>
#include <cstdint>

namespace yuspec::v1 {

enum class TK : uint16_t {
  // ─── Declaration keywords ───────────────────────────────────────────────
  KW_DEFINE,       // define
  KW_ENTITY,       // entity
  KW_COMPONENT,    // component
  KW_BEHAVIOR,     // behavior
  KW_FOR,          // for
  KW_STATEMACHINE, // statemachine
  KW_EVENT,        // event
  KW_WORKFLOW,     // workflow
  KW_STEP,         // step
  KW_ZONE,         // zone
  KW_SYSTEM,       // system
  KW_SCENARIO,     // scenario
  KW_IMPORT,       // import
  KW_RULE,         // rule

  // ─── Entity / property keywords ─────────────────────────────────────────
  KW_PROPERTY,     // property
  KW_HAS,          // has
  KW_DEFAULT,      // default
  KW_ACTOR,        // actor

  // ─── State machine keywords ──────────────────────────────────────────────
  KW_STATE,        // state
  KW_INITIAL,      // initial
  KW_TERMINAL,     // terminal
  KW_TIMEOUT,      // timeout
  KW_RETRY,        // retry
  KW_ON,           // on
  KW_FROM,         // from
  KW_WHEN,         // when
  KW_THEN,         // then
  KW_TRANSITION,   // transition

  // ─── Action keywords ────────────────────────────────────────────────────
  KW_SPAWN,        // spawn
  KW_EMIT,         // emit
  KW_SET,          // set
  KW_CALL,         // call
  KW_CREATE,       // create
  KW_DESTROY,      // destroy
  KW_ATTACH,       // attach
  KW_DETACH,       // detach
  KW_LOG,          // log
  KW_ASSERT,       // assert
  KW_WAIT,         // wait
  KW_FAIL,         // fail
  KW_EXPECT,       // expect
  KW_WITHIN,       // within

  // ─── Control flow ────────────────────────────────────────────────────────
  KW_IF,           // if
  KW_ELSE,         // else
  KW_WHILE,        // while
  KW_FOR_EACH,     // foreach
  KW_IN,           // in
  KW_BREAK,        // break
  KW_CONTINUE,     // continue
  KW_RETURN,       // return
  KW_LET,          // let

  // ─── Type keywords ───────────────────────────────────────────────────────
  KW_INT_TYPE,     // int
  KW_FLOAT_TYPE,   // float
  KW_BOOL_TYPE,    // bool
  KW_STRING_TYPE,  // string
  KW_DURATION_TYPE,// duration
  KW_LIST,         // list
  KW_MAP,          // map
  KW_ANY,          // any
  KW_VOID,         // void

  // ─── Literals ────────────────────────────────────────────────────────────
  LIT_INT,         // 42
  LIT_FLOAT,       // 3.14
  LIT_STRING,      // "hello"
  LIT_BOOL_TRUE,   // true
  LIT_BOOL_FALSE,  // false
  LIT_DURATION,    // 5s  2m  1h  300ms  2d
  LIT_NULL,        // null

  // ─── Identifiers ─────────────────────────────────────────────────────────
  IDENT,

  // ─── Operators ───────────────────────────────────────────────────────────
  PLUS,        // +
  MINUS,       // -
  STAR,        // *
  SLASH,       // /
  PERCENT,     // %
  EQ,          // ==
  NEQ,         // !=
  LT,          // <
  GT,          // >
  LTE,         // <=
  GTE,         // >=
  AND_AND,     // &&
  OR_OR,       // ||
  BANG,        // !
  ARROW,       // ->
  DOT,         // .
  ASSIGN,      // =
  PLUS_ASSIGN, // +=
  MINUS_ASSIGN,// -=
  COLON,       // :
  AT,          // @

  // ─── Punctuation ─────────────────────────────────────────────────────────
  SEMICOLON,   // ;
  COMMA,       // ,
  LBRACE,      // {
  RBRACE,      // }
  LPAREN,      // (
  RPAREN,      // )
  LBRACKET,    // [
  RBRACKET,    // ]
  X_DIM,       // x  (only in NxM terrain size)

  // ─── Misc ────────────────────────────────────────────────────────────────
  END_OF_FILE,
  UNKNOWN,
};

struct SrcPos {
  int line   = 1;
  int col    = 1;
  int offset = 0;
};

struct Token {
  TK          kind   = TK::UNKNOWN;
  std::string lexeme;
  SrcPos      pos;

  // Literal payloads
  int64_t     int_val  = 0;
  double      flt_val  = 0.0;
  double      dur_ms   = 0.0; // duration in milliseconds
};

const char* tk_name(TK k);

} // namespace yuspec::v1
