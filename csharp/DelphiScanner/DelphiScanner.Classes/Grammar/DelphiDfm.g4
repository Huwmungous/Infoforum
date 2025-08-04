// DelphiDfm.g4 (corrected)
grammar DelphiDfm;

options {
  language = CSharp;
  visitor = true;
  listener = true;
}

// Parser rules
dfmFile: (genericProperty | objectDeclaration)* EOF;

objectDeclaration
    : 'object' objectName=IDENTIFIER ':' className=IDENTIFIER objectBody 'end'
    ;

// Fixed: Allow properties and objects to be intermixed
objectBody
    : (property | objectDeclaration)*
    ;

property
    : sqlProperty
    | genericProperty
    ;

sqlProperty
    : 'SQL' '.' 'Strings' '=' queryText=stringList
    ;

genericProperty
    : IDENTIFIER '=' value
    ;

value
    : STRING                           # stringValue
    | IDENTIFIER                       # identifierValue
    | BOOLEAN                          # booleanValue
    | NUMBER                           # numberValue
    | stringList                       # stringListValue
    | arrayValue                       # arrayValueType
    ;

stringList
    : '(' stringListItems? ')'
    ;

// Add array value support
arrayValue
    : '[' arrayItems? ']'
    ;

arrayItems
    : IDENTIFIER (',' IDENTIFIER)*
    ;

stringListItems
    : STRING (',' STRING)*
    | STRING+
    ;

// Lexer rules
BOOLEAN
    : 'True'
    | 'False'
    ;

STRING
    : '\'' ( ~'\'' | '\'\'' )* '\''
    ;

IDENTIFIER
    : [a-zA-Z_][a-zA-Z0-9_.]*
    ;

NUMBER
    : [0-9]+
    ;

WS
    : [ \t\r\n]+ -> skip
    ;

BLOCK_COMMENT
    : '{' .*? '}' -> skip
    ;

LINE_COMMENT
    : '//' ~[\r\n]* -> skip
    ;

// Remove these duplicate token definitions - they're causing conflicts
// OBJECT : 'object';
// END    : 'end';
// EQUALS : '=';
// LPAREN : '(';
// RPAREN : ')';
// COMMA  : ',';
// SEMI   : ';';
// COLON  : ':';
// DOT    : '.';