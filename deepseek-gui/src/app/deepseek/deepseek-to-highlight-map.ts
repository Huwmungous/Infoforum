// deepseek-to-highlight-map.ts

/**
 * Mapping from deepseek-coder:33b language identifiers (keys)
 * to the corresponding highlight.js language names (values).
 *
 * For entries where the deepseek string is identical to the highlight.js identifier,
 * the mapping is provided as a commented-out line (for reference).
 */
export const deepseekToHighlightMap: { [key: string]: string } = {
  // "c": "c",                   // identical, no mapping needed
  "c#": "csharp",               // deepseek "c#" -> highlight.js "csharp"
  // "csharp": "csharp",         // identical, no mapping needed
  "c++": "cpp",                 // deepseek "c++" -> highlight.js "cpp"
  // "cpp": "cpp",               // identical, no mapping needed
  "objective-c": "objectivec",  // deepseek "objective-c" -> highlight.js "objectivec"
  // "objectivec": "objectivec", // identical, no mapping needed

  // "java": "java",             // identical, no mapping needed
  // "javascript": "javascript", // identical, no mapping needed
  "js": "javascript",           // deepseek "js" -> highlight.js "javascript"
  
  // "typescript": "typescript", // identical, no mapping needed
  "ts": "typescript",           // deepseek "ts" -> highlight.js "typescript"

  // "python": "python",         // identical, no mapping needed
  // "ruby": "ruby",             // identical, no mapping needed
  // "php": "php",               // identical, no mapping needed
  // "perl": "perl",             // identical, no mapping needed
  // "lua": "lua",               // identical, no mapping needed
  // "bash": "bash",             // identical, no mapping needed
  "shell": "bash",              // deepseek "shell" -> highlight.js "bash"
  
  "html": "xml",                // deepseek "html" -> highlight.js "xml"
  // "xml": "xml",               // identical, no mapping needed
  // "css": "css",               // identical, no mapping needed

  // "swift": "swift",           // identical, no mapping needed
  // "kotlin": "kotlin",         // identical, no mapping needed
  // "go": "go",                 // identical, no mapping needed
  // "rust": "rust",             // identical, no mapping needed
  // "scala": "scala",           // identical, no mapping needed
  // "sql": "sql",               // identical, no mapping needed
  // "dart": "dart",             // identical, no mapping needed
  // "r": "r",                   // identical, no mapping needed
  // "elixir": "elixir",         // identical, no mapping needed
  // "haskell": "haskell",       // identical, no mapping needed

  // "markdown": "markdown",     // identical, no mapping needed
  "md": "markdown"              // deepseek "md" -> highlight.js "markdown"
};

export function mapDeepseekToHighlight(language: string): string {
  return deepseekToHighlightMap[language] || language;
}
