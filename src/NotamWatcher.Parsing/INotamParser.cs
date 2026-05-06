using NotamWatcher.Parsing.Models;

namespace NotamWatcher.Parsing;

public interface INotamParser
{
    ParseResult<ParsedNotam> Parse(string rawText);
}
