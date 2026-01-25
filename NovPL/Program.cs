using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Running;
using NoNPL;
using NoNPL.Benchmark;
using NoNPL.DataReaders;
using System.Diagnostics;

var tokenizer = new BPETokenizer(@"'(?:[sdmt]|ll|ve|re)| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+(?!\S)|\s+");

await tokenizer.Train(
    "C:\\Users\\nikit\\OneDrive\\Desktop\\raznoe\\Projects\\CS336\\NovPL\\NovPL\\Datasets\\TinyStories-test.txt",
    "<|endoftext|>", 
    404,
    Environment.ProcessorCount);
/*
var config = DefaultConfig.Instance
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .AddExporter(MarkdownExporter.Default)
            .AddExporter(HtmlExporter.Default)
            .AddExporter(CsvExporter.Default);

var summary = BenchmarkRunner.Run<BPETokenizer>(config);*/