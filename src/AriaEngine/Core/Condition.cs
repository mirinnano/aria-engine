namespace AriaEngine.Core;

/// <summary>
/// 条件式の種類
/// </summary>
public enum ConditionType
{
    Compare,      // %0 == 1
    DirectBool,   // %0 (truthy check)
}

/// <summary>
/// 条件式の項（単一の比較またはANDコネクタ）
/// </summary>
public readonly struct ConditionTerm
{
    public string Lhs { get; init; }
    public string Op { get; init; }   // "==", "!=", ">", "<", ">=", "<=", "&&"
    public string Rhs { get; init; }

    public ConditionTerm(string lhs, string op, string rhs)
    {
        Lhs = lhs;
        Op = op;
        Rhs = rhs;
    }

    public bool IsAndConnector => Op == "&&";
}

/// <summary>
/// 構造化された条件式
/// Parserで事前変換され、実行時の文字列パースを不要にする
/// </summary>
public readonly struct Condition
{
    public IReadOnlyList<ConditionTerm> Terms { get; init; }

    public Condition(IReadOnlyList<ConditionTerm> terms)
    {
        Terms = terms;
    }

    public bool IsEmpty => (Terms == null || Terms.Count == 0) && Expression == null;

    /// <summary>
    /// 新しい式評価システム用のAST（移行期間: TermsまたはExpressionのどちらかが有効）
    /// </summary>
    public Expression? Expression { get; init; }

    /// <summary>
    /// 旧式の文字列トークンリストに逆変換（シリアライズ用）
    /// </summary>
    public List<string> ToTokenList()
    {
        if (IsEmpty) return new List<string>();
        var tokens = new List<string>();
        foreach (var term in Terms)
        {
            if (term.IsAndConnector)
            {
                tokens.Add("&&");
            }
            else if (term.Op == "truthy")
            {
                tokens.Add(term.Lhs);
                tokens.Add("&&");
            }
            else
            {
                tokens.Add(term.Lhs);
                tokens.Add(term.Op);
                tokens.Add(term.Rhs);
            }
        }
        return tokens;
    }

    /// <summary>
    /// 旧式の文字列トークンリストからConditionを構築（移行期間用）
    /// </summary>
    public static Condition FromTokens(IReadOnlyList<string> tokens)
    {
        if (tokens == null || tokens.Count == 0)
            return new Condition(Array.Empty<ConditionTerm>());

        // 新しい式システムを試行: 算術演算子や配列アクセスが含まれるか確認
        var expr = ExpressionParser.TryParse(tokens);
        if (expr != null)
        {
            return new Condition { Expression = expr };
        }

        // フォールバック: 従来のフラットな条件式パース
        var terms = new List<ConditionTerm>();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] == "&&")
            {
                terms.Add(new ConditionTerm("", "&&", ""));
                continue;
            }

            string lhs = tokens[i];
            string op = (i + 1 < tokens.Count) ? tokens[i + 1] : "truthy";
            
            if (op == "&&")
            {
                terms.Add(new ConditionTerm(lhs, "truthy", "0"));
                continue;
            }

            string rhs = (i + 2 < tokens.Count) ? tokens[i + 2] : "0";
            terms.Add(new ConditionTerm(lhs, op, rhs));
            i += 2;
        }

        return new Condition(terms);
    }
}
