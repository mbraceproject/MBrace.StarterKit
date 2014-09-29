open System
open System.IO
open System.Text.RegularExpressions

/// Word count : token x frequency counts
type WordCount = (string * int) []

/// Word count utilities
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module WordCount =
    
    /// empty wordcount ; word frequency identity element.
    let empty : WordCount = [||]

    /// <summary>
    ///     Wordcount reduce function; defines a commutative monoid over word frequencies.
    /// </summary>
    /// <param name="wc">First frequency count</param>
    /// <param name="wc'">Second frequency count</param>
    let reduce (wc : WordCount) (wc': WordCount) : WordCount = 
        Seq.append wc wc'
        |> Seq.groupBy fst 
        |> Seq.map (fun (key, value) -> (key, value |> Seq.sumBy snd ))
        |> Seq.sortBy (fun (_,t) -> -t)
        |> Seq.toArray

    /// tokens ignored by wordcount
    let private noiseWords = 
        set [
            "a"; "about"; "above"; "all"; "along"; "also"; "although"; "am"; "an"; "any"; "are"; "aren't"; "as"; "at";
            "be"; "because"; "been"; "but"; "by"; "can"; "cannot"; "could"; "couldn't"; "did"; "didn't"; "do"; "does"; 
            "doesn't"; "e.g."; "either"; "etc"; "etc."; "even"; "ever";"for"; "from"; "further"; "get"; "gets"; "got"; 
            "had"; "hardly"; "has"; "hasn't"; "having"; "he"; "hence"; "her"; "here"; "hereby"; "herein"; "hereof"; 
            "hereon"; "hereto"; "herewith"; "him"; "his"; "how"; "however"; "I"; "i.e."; "if"; "into"; "it"; "it's"; "its";
            "me"; "more"; "most"; "mr"; "my"; "near"; "nor"; "now"; "of"; "onto"; "other"; "our"; "out"; "over"; "really"; 
            "said"; "same"; "she"; "should"; "shouldn't"; "since"; "so"; "some"; "such"; "than"; "that"; "the"; "their"; 
            "them"; "then"; "there"; "thereby"; "therefore"; "therefrom"; "therein"; "thereof"; "thereon"; "thereto"; 
            "therewith"; "these"; "they"; "this"; "those"; "through"; "thus"; "to"; "too"; "under"; "until"; "unto"; "upon";
            "us"; "very"; "viz"; "was"; "wasn't"; "we"; "were"; "what"; "when"; "where"; "whereby"; "wherein"; "whether";
            "which"; "while"; "who"; "whom"; "whose"; "why"; "with"; "without"; "would"; "you"; "your" ; "have"; "thou"; "will"; 
            "shall"
        ]

    /// recognizes tokens that are words
    let private wordRegex = new System.Text.RegularExpressions.Regex("^[a-z]*$")

    /// <summary>
    ///     Computes the wordcount for provided text.
    /// </summary>
    /// <param name="text">Input text.</param>
    let compute (text : string) : WordCount =
        let words = text.Split([|' '; '.'; ','|], StringSplitOptions.RemoveEmptyEntries)
        words
        |> Seq.map (fun word -> word.ToLower().Trim())
        |> Seq.filter (fun word -> word.Length > 3 && wordRegex.IsMatch word && not <| noiseWords.Contains word)
        |> Seq.groupBy id
        |> Seq.map (fun (key, values) -> key, Seq.length values)
        |> Seq.toArray