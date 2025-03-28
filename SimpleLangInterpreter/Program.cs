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
            Console.WriteLine(EvaluateExpression(content));
        }
        else if (Regex.IsMatch(line, "^VAR [a-zA-Z_][a-zA-Z0-9_]* = .+$"))
        {
            string[] parts = line.Substring(4).Split(new[] { " = " }, StringSplitOptions.None);
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
                
                variables[varName] = EvaluateExpression(value) ?? "null";
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
            foreach (var variable in variables)
            {
                expression = expression.Replace(variable.Key, variable.Value.ToString() ?? "null");
            }
            
            if (Regex.IsMatch(expression, "^\\d+$")) return int.Parse(expression);
            if (Regex.IsMatch(expression, "^\\d+\\.\\d+$")) return float.Parse(expression);
            if (Regex.IsMatch(expression, "^'(.*)'$") || Regex.IsMatch(expression, "^\"(.*)\"$"))
                return expression.Substring(1, expression.Length - 2)
                                 .Replace("\\", "\\\\")
                                 .Replace("\\n", "\n")
                                 .Replace("\\t", "\t")
                                 .Replace("\\\"", "\"")
                                 .Replace("\\'", "'");
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
