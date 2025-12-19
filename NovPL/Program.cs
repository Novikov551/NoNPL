using NoNPL;
using NoNPL.DataReaders;
using System.Diagnostics;

var tokenizer = new BPETokenizer(@"'(?:[sdmt]|ll|ve|re)| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+", 10000, 10000);

await tokenizer.Train("C:\\Users\\nikit\\OneDrive\\Desktop\\raznoe\\Projects\\CS336\\NovPL\\NovPL\\Datasets\\TinyStories-train.txt",
    "<|endoftext|>",
    12);

