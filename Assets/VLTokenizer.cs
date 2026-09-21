using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// VLM용 토크나이저
/// HuggingFace tokenizer.json 형식 지원
/// </summary>
[Serializable]
public class VLTokenizer
{
    private Dictionary<string, int> vocab;
    private Dictionary<int, string> idToToken;
    private string[] vocabArray;
    
    public int EOSToken { get; private set; }
    public int BOSToken { get; private set; }
    
    public VLTokenizer(string tokenizerJson)
    {
        ParseTokenizer(tokenizerJson);
    }
    
    private void ParseTokenizer(string json)
    {
        vocab = new Dictionary<string, int>();
        idToToken = new Dictionary<int, string>();
        
        try
        {
            // 간단한 JSON 파싱 (vocab 부분 추출)
            // 실제 구현에서는 JSON.NET 또는 Unity의 JsonUtility 사용 가능
            
            // 임시: 기본 BPE 토크나이저 구조 가정
            // tokenizer.json의 model.vocab에서 토큰 로드
            
            int idx = json.IndexOf("\"model\":");
            if (idx >= 0)
            {
                // vocab 찾기
                int vocabStart = json.IndexOf("\"vocab\":", idx);
                if (vocabStart < 0) vocabStart = json.IndexOf("\"vocabulary\":", idx);
                
                if (vocabStart > 0)
                {
                    ParseVocab(json, vocabStart);
                }
            }
            
            // vocab을 찾지 못한 경우 기본값 설정
            if (vocab.Count == 0)
            {
                SetupDefaultVocab();
            }
            
            // EOS/ BOS 토큰 설정
            EOSToken = FindTokenId("<|endoftext|>");
            BOSToken = FindTokenId("<|startoftext|>");
            
            if (EOSToken < 0) EOSToken = FindTokenId("</s>");
            if (BOSToken < 0) BOSToken = FindTokenId("<s>");
            if (EOSToken < 0) EOSToken = 0; // 기본값
            if (BOSToken < 0) BOSToken = 1;
            
            vocabArray = new string[idToToken.Count];
            foreach (var kvp in idToToken)
            {
                if (kvp.Key >= 0 && kvp.Key < vocabArray.Length)
                    vocabArray[kvp.Key] = kvp.Value;
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[VLTokenizer] 토크나이저 파싱 오류: {e.Message}");
            SetupDefaultVocab();
        }
    }
    
    private void ParseVocab(string json, int startIdx)
    {
        // { "token": id } 또는 [ ["token", id] ] 형식 파싱
        int braceCount = 0;
        bool inString = false;
        int valueStart = -1;
        int valueEnd = -1;
        string currentToken = null;
        
        for (int i = startIdx; i < json.Length; i++)
        {
            char c = json[i];
            
            if (c == '"' && (i == 0 || json[i - 1] != '\\'))
            {
                inString = !inString;
                continue;
            }
            
            if (inString) continue;
            
            if (c == '{') braceCount++;
            else if (c == '}')
            {
                braceCount--;
                if (braceCount == 0 && currentToken != null && valueStart >= 0)
                {
                    string valueStr = json.Substring(valueStart, valueEnd - valueStart + 1).Trim();
                    if (int.TryParse(valueStr, out int tokenId))
                    {
                        vocab[currentToken] = tokenId;
                        idToToken[tokenId] = currentToken;
                    }
                    currentToken = null;
                    valueStart = -1;
                }
            }
            else if (braceCount == 1 && c == ':' && currentToken != null)
            {
                valueStart = i + 1;
            }
            else if (valueStart >= 0 && char.IsDigit(c))
            {
                valueEnd = i;
            }
        }
    }
    
    private void SetupDefaultVocab()
    {
        // 기본 토큰 설정 (실제 사용 시 토크나이저 파일 필요)
        vocab = new Dictionary<string, int>();
        idToToken = new Dictionary<int, string>();
        
        // 일반적인 BPE 토큰
        string[] defaultTokens = new string[]
        {
            "<|endoftext|>", "<|startoftext|>", "</s>", "<s>",
            " hello", " world", " the", " a", " is", " t", " you", " to",
            "ing", " ion", " ed", " th", " an", " er", " es", " on",
            " i", " y", " o", " u", " e", " n", " s", " l",
            " r", " h", " d", " c", " m", " f", " g", " p",
            " b", " v", " k", " w", " z", " j", " x", " q",
            "!", "?", ".", ",", "'", "\"", " ", "\n",
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9"
        };
        
        for (int i = 0; i < defaultTokens.Length; i++)
        {
            vocab[defaultTokens[i]] = i;
            idToToken[i] = defaultTokens[i];
        }
        
        EOSToken = 0;
        BOSToken = 1;
    }
    
    private int FindTokenId(string token)
    {
        if (vocab.TryGetValue(token, out int id))
            return id;
        return -1;
    }
    
    /// <summary>
    /// 텍스트를 토큰 ID로 변환
    /// </summary>
    public int[] Encode(string text)
    {
        var tokens = new List<int>();
        
        // BOF 토큰 추가
        tokens.Add(BOSToken);
        
        // 간단한 토큰화 (실제 구현에서는 BPE/WordPiece 사용)
        string[] words = text.ToLower().Split(' ');
        
        foreach (string word in words)
        {
            if (string.IsNullOrWhiteSpace(word)) continue;
            
            // 단어가 정확히 있으면 사용
            if (vocab.TryGetValue(word, out int exactId))
            {
                tokens.Add(exactId);
            }
            else
            {
                // 문자 단위 토큰화
                foreach (char c in word)
                {
                    string charStr = c.ToString();
                    if (vocab.TryGetValue(charStr, out int charId))
                    {
                        tokens.Add(charId);
                    }
                }
            }
            
            // 공백 토큰
            if (vocab.TryGetValue(" ", out int spaceId))
            {
                tokens.Add(spaceId);
            }
        }
        
        // EOS 토큰 추가
        tokens.Add(EOSToken);
        
        return tokens.ToArray();
    }
    
    /// <summary>
    /// 토큰 ID를 텍스트로 변환
    /// </summary>
    public string Decode(List<int> tokenIds)
    {
        if (tokenIds == null || tokenIds.Count == 0)
            return "";
        
        var result = new System.Text.StringBuilder();
        
        foreach (int id in tokenIds)
        {
            if (id == EOSToken || id == BOSToken)
                continue;
            
            if (id >= 0 && id < vocabArray.Length && vocabArray[id] != null)
            {
                string token = vocabArray[id];
                
                // 공백 처리
                if (token == " ")
                {
                    // 그대로 추가
                }
                else if (token.StartsWith(" ") || result.Length == 0)
                {
                    result.Append(token);
                }
                else
                {
                    result.Append(token);
                }
            }
        }
        
        return result.ToString().Trim();
    }
    
    /// <summary>
    /// 어휘 크기 반환
    /// </summary>
    public int VocabSize => vocabArray?.Length ?? 0;
}
