using SemanticTranscriptProcessor.Common.Common.Model;

namespace SimAlign.Core.Alignment;

public class TokenMapper
{
    private readonly TokenType _tokenType;
    private readonly List<int> _srcTokenMap;
    private readonly List<int> _trgTokenMap;

    public TokenMapper(TokenType tokenType, List<int> srcTokenMap, List<int> trgTokenMap)
    {
        _tokenType = tokenType;
        _srcTokenMap = srcTokenMap;
        _trgTokenMap = trgTokenMap;
    }

    public (int SrcIndex, int TrgIndex) MapIndices(int srcTokenIndex, int trgTokenIndex)
    {
        int srcIndex = _tokenType == TokenType.BPE ? _srcTokenMap[srcTokenIndex] : srcTokenIndex;
        int trgIndex = _tokenType == TokenType.BPE ? _trgTokenMap[trgTokenIndex] : trgTokenIndex;
        return (srcIndex, trgIndex);
    }
}