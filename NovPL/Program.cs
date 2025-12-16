using NoNPL;

var tokenizer = new BPETokenizer(@"'(?:[sdmt]|ll|ve|re)| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+", 10000, 10000);

tokenizer.Train();


