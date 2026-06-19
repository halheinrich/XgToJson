using XgToJson;

// XgToJson — convert XG-format (.xg/.xgp) files to JSON decision records.
//
//   XgToJson <input> [outputDir]
//
//     <input>     a .xg/.xgp file, or a directory of them (top-level only).
//     [outputDir] an existing directory to write .json output into;
//                 defaults to the current directory.
//
// Exit codes: 0 = success · 1 = usage/argument error · 2 = conversion failure.
//
// The whole CLI contract lives in CliRunner.Run; this entry point only supplies
// the process-global ambient dependencies (the console streams and the working
// directory) so that the contract is testable in-process without them.
return CliRunner.Run(args, Console.Out, Console.Error, Directory.GetCurrentDirectory());
