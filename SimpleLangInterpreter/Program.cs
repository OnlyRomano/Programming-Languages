using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Data;

class SimpleLangInterpreter
{
    static Dictionary<string, object> variables = new Dictionary<string, object>();
    static readonly HashSet<string> keywords = new HashSet<string>
    {
        "VAR", "IF", "ELSE", "WHILE", "RETURN", "FUNC", "PRINT", "END", "BEGIN"
    };

    static void Main()
    {
        Console.WriteLine("Enter your code (type 'exit' to quit):");
        while (true)
        {
            string? input = Console.ReadLine();
            if (string.IsNullOrEmpty(input)) continue;
            if (input.ToLower() == "exit") break;
            ExecuteLines(input);
        }
    }

    static void ExecuteLines(string input)
    {
        string[] lines = input.Split(new[] { "!" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (!string.IsNullOrEmpty(line) && !line.StartsWith("//") && !line.StartsWith("/*"))
            {
                ExecuteLine(line);
            }
        }
    }

    static void ExecuteLine(string line)
    {
        if (line.StartsWith("PRINT "))
        {
            string content = line.Substring(6).Trim();
            object? result = EvaluateExpression(content);
            Console.WriteLine(result?.ToString() ?? "null");
        }
        else if (Regex.IsMatch(line, "^VAR [a-zA-Z_][a-zA-Z0-9_]* = .+$"))
        {
            string[] parts = line.Substring(4).Split(new[] { " = " }, 2, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                string varName = parts[0].Trim();
                string value = parts[1].Trim();

                if (keywords.Contains(varName))
                {
                    Console.WriteLine("Syntax Error: Cannot use a keyword as a variable name!");
                    return;
                }
                
                if (!Regex.IsMatch(varName, "^[a-zA-Z_][a-zA-Z0-9_]*$") || keywords.Contains(varName))
                {
                    Console.WriteLine("Syntax Error: Invalid identifier name!");
                    return;
                }

                // Handle string literals directly
                if (value.StartsWith("\"") && value.EndsWith("\"") || value.StartsWith("'") && value.EndsWith("'"))
                {
                    string content = value.Substring(1, value.Length - 2);
                    variables[varName] = content.Replace("\\n", "\n")
                                             .Replace("\\t", "\t")
                                             .Replace("\\r", "\r")
                                             .Replace("\\\"", "\"")
                                             .Replace("\\'", "'")
                                             .Replace("\\\\", "\\");
                }
                else
                {
                    object? evaluatedValue = EvaluateExpression(value);
                    variables[varName] = evaluatedValue ?? "null";
                }
            }
        }
        else
        {
            Console.WriteLine("Syntax Error: Unrecognized statement!");
        }
    }

    static object? EvaluateExpression(string expression)
    {
        try
        {
            // First check if it's a variable reference
            if (variables.ContainsKey(expression))
            {
                return variables[expression];
            }

            // Handle variable substitution in expressions
            foreach (var variable in variables)
            {
                expression = Regex.Replace(expression, $"\\b{variable.Key}\\b", variable.Value?.ToString() ?? "null");
            }
            
            if (Regex.IsMatch(expression, "^\\d+$")) return int.Parse(expression);
            if (Regex.IsMatch(expression, "^\\d+\\.\\d+$")) return float.Parse(expression);
            if (Regex.IsMatch(expression, "^'(.*?)'$") || Regex.IsMatch(expression, "^\"(.*?)\"$"))
            {
                string content = expression.Substring(1, expression.Length - 2);
                return content.Replace("\\n", "\n")
                            .Replace("\\t", "\t")
                            .Replace("\\r", "\r")
                            .Replace("\\\"", "\"")
                            .Replace("\\'", "'")
                            .Replace("\\\\", "\\");
            }
            if (expression == "true" || expression == "false") return bool.Parse(expression);
            if (expression == "null" || expression == "undefined") return "null";
            
            DataTable dt = new DataTable();
            return dt.Compute(expression, "");
        }
        catch
        {
            return "Syntax Error: Invalid Expression!";
        }
    }
}
