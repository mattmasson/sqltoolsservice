'use strict';

import * as vscode from 'vscode';
//import * as path from 'path';

import {
	LanguageClient,
	LanguageClientOptions,
	ServerOptions,
	TransportKind
} from 'vscode-languageclient';

let client: LanguageClient;

export function activate(context: vscode.ExtensionContext) {

    let serverExecutablePath = "D:/src/github/sqltoolsservice/samples/vscode-lsp/server/Microsoft.SqlTools.Samples.LanguageServerHost/bin/Debug/netcoreapp2.1/Microsoft.SqlTools.Samples.LanguageServerHost.dll";

	// The debug options for the server
	// --inspect=6009: runs the server in Node's Inspector mode so VS Code can attach to the server for debugging
    let debugOptions = [serverExecutablePath, "--enable-logging", "--log-dir", "C:/Temp/logs", "--wait-for-debugger"];
    
	// If the extension is launched in debug mode then the debug server options are used
	// Otherwise the run options are used
	let serverOptions: ServerOptions = {
		run: { command: "dotnet", args: [serverExecutablePath], transport: TransportKind.stdio },
		debug: { command: "dotnet", args: debugOptions, transport: TransportKind.stdio },
	};

	// Options to control the language client
	let clientOptions: LanguageClientOptions = {        
		// Register the server for plain text documents
		documentSelector: [{ scheme: 'file', language: 'plaintext' }],
		synchronize: {
			// Notify the server about file changes to '.clientrc files contained in the workspace
			fileEvents: vscode.workspace.createFileSystemWatcher('**/.clientrc')
        },        
        outputChannel: new CustomOutputChannel("lsp")
	};

	// Create the language client and start the client.
	client = new LanguageClient(
		'languageServerExample',
		'Language Server Example',
		serverOptions,
		clientOptions
	);

	// Start the client. This will also launch the server
	client.start();
}

class CustomOutputChannel implements vscode.OutputChannel {
    name: string;

    constructor(name: string) {
        this.name = name;
    }

    append(value: string): void {
        console.log(value);
    }
    appendLine(value: string): void {
        console.log(value);
    }
    // tslint:disable-next-line:no-empty
    clear(): void {
    }
    show(preserveFocus?: boolean): void;
    show(column?: vscode.ViewColumn, preserveFocus?: boolean): void;
    // tslint:disable-next-line:no-empty
    show(column?: any, preserveFocus?: any): void {
    }
    // tslint:disable-next-line:no-empty
    hide(): void {
    }
    // tslint:disable-next-line:no-empty
    dispose(): void {
    }
}