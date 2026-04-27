import * as vscode from 'vscode';

const keywords: Record<string, string> = {
  entity: 'Declares a gameplay entity and its typed property bag.',
  on: 'Declares an event handler or a state transition trigger.',
  when: 'Adds a condition that must pass before a handler runs.',
  behavior: 'Declares a state machine behavior for an entity type.',
  state: 'Declares a state inside a behavior.',
  scenario: 'Declares a lightweight scenario test.',
  given: 'Sets up scenario preconditions.',
  expect: 'Checks scenario outcomes.',
  every: 'Runs actions on a repeated interval inside a state.',
  has: 'Checks whether an entity contains a value.',
  set: 'Assigns an entity property.',
  do: 'Starts a state action block.',
  with: 'Adds a target entity type to an event handler.',
  from: 'Binds an entity declaration to a ScriptableObject asset.',
  dialogue: 'Declares a lightweight dialogue block.',
  line: 'Adds a dialogue line.',
  choice: 'Adds a dialogue choice and target.',
  for: 'Binds a behavior or dialogue to an entity type.',
  start_dialogue: 'Starts a named dialogue block.',
  end: 'Ends a dialogue branch.'
};

const keywordItems = Object.keys(keywords).map(keyword => {
  const item = new vscode.CompletionItem(keyword, vscode.CompletionItemKind.Keyword);
  item.detail = 'YUSPEC keyword';
  item.documentation = keywords[keyword];
  return item;
});

const diagnostics = vscode.languages.createDiagnosticCollection('yuspec');

export function activate(context: vscode.ExtensionContext) {
  context.subscriptions.push(diagnostics);

  context.subscriptions.push(vscode.languages.registerCompletionItemProvider('yuspec', {
    provideCompletionItems: () => keywordItems
  }));

  context.subscriptions.push(vscode.languages.registerHoverProvider('yuspec', {
    provideHover(document, position) {
      const range = document.getWordRangeAtPosition(position, /[A-Za-z_][A-Za-z0-9_]*/);
      if (!range) {
        return undefined;
      }

      const word = document.getText(range);
      const description = keywords[word];
      return description ? new vscode.Hover(`**${word}**\n\n${description}`, range) : undefined;
    }
  }));

  context.subscriptions.push(vscode.languages.registerDefinitionProvider('yuspec', {
    provideDefinition(document, position) {
      const range = document.getWordRangeAtPosition(position, /[A-Z][A-Za-z0-9_]*/);
      if (!range) {
        return undefined;
      }

      const entityName = document.getText(range);
      const declaration = findEntityDeclaration(document, entityName);
      return declaration ? new vscode.Location(document.uri, declaration) : undefined;
    }
  }));

  context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(document => {
    if (document.languageId === 'yuspec') {
      validateDocument(document);
    }
  }));

  for (const document of vscode.workspace.textDocuments) {
    if (document.languageId === 'yuspec') {
      validateDocument(document);
    }
  }
}

export function deactivate() {
  diagnostics.clear();
  diagnostics.dispose();
}

function validateDocument(document: vscode.TextDocument) {
  const items: vscode.Diagnostic[] = [];
  const stack: vscode.Position[] = [];

  for (let lineIndex = 0; lineIndex < document.lineCount; lineIndex++) {
    const text = stripComment(document.lineAt(lineIndex).text);
    let inString = false;

    for (let column = 0; column < text.length; column++) {
      const char = text[column];
      if (char === '"') {
        inString = !inString;
        continue;
      }

      if (inString) {
        continue;
      }

      if (char === '{') {
        stack.push(new vscode.Position(lineIndex, column));
      } else if (char === '}') {
        if (stack.length === 0) {
          const range = new vscode.Range(lineIndex, column, lineIndex, column + 1);
          items.push(new vscode.Diagnostic(range, 'Unmatched closing brace.', vscode.DiagnosticSeverity.Error));
        } else {
          stack.pop();
        }
      }
    }
  }

  for (const position of stack) {
    const range = new vscode.Range(position, position.translate(0, 1));
    items.push(new vscode.Diagnostic(range, 'Unmatched opening brace.', vscode.DiagnosticSeverity.Error));
  }

  diagnostics.set(document.uri, items);
}

function findEntityDeclaration(document: vscode.TextDocument, entityName: string): vscode.Range | undefined {
  const pattern = new RegExp(`^\\s*entity\\s+${escapeRegExp(entityName)}(?:\\s+from\\s+"[^"]+")?\\s*\\{`);
  for (let lineIndex = 0; lineIndex < document.lineCount; lineIndex++) {
    const text = document.lineAt(lineIndex).text;
    const match = pattern.exec(text);
    if (match) {
      const start = text.indexOf(entityName);
      return new vscode.Range(lineIndex, start, lineIndex, start + entityName.length);
    }
  }

  return undefined;
}

function stripComment(line: string): string {
  let inString = false;
  for (let index = 0; index < line.length; index++) {
    const char = line[index];
    if (char === '"') {
      inString = !inString;
      continue;
    }

    if (inString) {
      continue;
    }

    if (char === '#') {
      return line.substring(0, index);
    }

    if (char === '/' && line[index + 1] === '/') {
      return line.substring(0, index);
    }
  }

  return line;
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
