﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using JSON_Tools.JSON_Tools;
using JSON_Tools.Utils;
using Kbg.NppPluginNET.PluginInfrastructure;

namespace JSON_Tools.Tests
{
    /// <summary>
    /// contains benchmarking tests for RemesPath, JsonParser, and JsonSchemaValidator
    /// </summary>
    public class Benchmarker
    {
        /// <summary>
        /// Repeatedly parse the JSON of a large file (big_random.json, about 1MB, containing nested arrays, dicts,
        /// with ints, floats and strings as scalars)<br></br>
        /// Also repeatedly run Remespath queries on the JSON.<br></br>
        /// queries_and_descriptions is an array of 2-string arrays (query, description)<br></br>
        /// For the most recent benchmarking results, see "most recent errors.txt" in the main repository.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="num_parse_trials"></param>
        public static void BenchmarkParserAndRemesPath(string[][] queries_and_descriptions, 
                                     string fname,
                                     int num_parse_trials,
                                     int num_query_trials)
        {
            // setup
            JsonParser jsonParser = new JsonParser();
            Stopwatch watch = new Stopwatch();
            if (!File.Exists(fname))
            {
                Npp.AddLine($"Can't run benchmark tests because file {fname}\ndoes not exist");
                return;
            }
            string jsonstr = File.ReadAllText(fname);
            int len = jsonstr.Length;
            long[] load_times = new long[num_parse_trials];
            JNode json = new JNode();
            // benchmark time to load json
            for (int ii = 0; ii < num_parse_trials; ii++)
            {
                watch.Reset();
                watch.Start();
                try
                {
                    json = jsonParser.Parse(jsonstr);
                }
                catch (Exception ex)
                {
                    Npp.AddLine($"Couldn't run benchmark tests because parsing error occurred:\n{ex}");
                    return;
                }
                watch.Stop();
                long t = watch.Elapsed.Ticks;
                load_times[ii] = t;
            }
            // display loading results
            string json_preview = json.ToString().Slice(":300") + "\n...";
            Npp.AddLine($"Preview of json: {json_preview}");
            (double mean, double sd) = GetMeanAndSd(load_times);
            Npp.AddLine($"To convert JSON string of size {len} into JNode took {ConvertTicks(mean)} +/- {ConvertTicks(sd)} " +
                $"ms over {load_times.Length} trials");
            var load_times_str = new string[load_times.Length];
            for (int ii = 0; ii < load_times.Length; ii++)
            {
                load_times_str[ii] = (load_times[ii] / 10000).ToString();
            }
            Npp.AddLine($"Load times (ms): {String.Join(", ", load_times_str)}");
            // time query compiling
            RemesParser parser = new RemesParser();
            foreach (string[] query_and_desc in queries_and_descriptions)
            {
                string query = query_and_desc[0];
                string description = query_and_desc[1];
                Npp.AddLine($@"=========================
Performance tests for RemesPath ({description})
=========================
");
                JNode query_func = null;
                long[] compile_times = new long[num_query_trials];
                for (int ii = 0; ii < num_query_trials; ii++)
                {
                    watch.Reset();
                    watch.Start();
                    try
                    {
                        (List<object> sel_toks, List<object> mut_toks) = parser.lexer.Tokenize(query);
                        query_func = parser.Compile(sel_toks, mut_toks);
                    }
                    catch (Exception ex)
                    {
                        Npp.AddLine($"Couldn't run RemesPath benchmarks because of error while compiling query:\n{ex}");
                        return;
                    }
                    watch.Stop();
                    long t = watch.Elapsed.Ticks;
                    compile_times[ii] = t;
                }
                (mean, sd)= GetMeanAndSd(compile_times);
                double comp_mean = ConvertTicks(mean, "mus");
                double comp_sd = ConvertTicks(sd, "mus");
                Npp.AddLine($"Compiling query \"{query}\" into took {comp_mean} +/- {comp_sd} microseconds over {num_query_trials} trials");
                // time query execution
                long[] query_times = new long[num_query_trials];
                JNode result = new JNode();
                for (int ii = 0; ii < num_query_trials; ii++)
                {
                    watch.Reset();
                    watch.Start();
                    try
                    {
                        if (query_func is CurJson selector)
                        {
                            result = selector.function(json);
                        }
                        else if (query_func is JMutator mutator)
                        {
                            result = mutator.Mutate(json);
                        }
                    }
                    catch (Exception ex)
                    {
                        Npp.AddLine($"Couldn't run RemesPath benchmarks because of error while executing compiled query:\n{ex}");
                    }
                    watch.Stop();
                    long t = watch.Elapsed.Ticks;
                    query_times[ii] = t;
                }
                // display querying results
                (mean, sd) = GetMeanAndSd(query_times);
                Npp.AddLine($"To run pre-compiled query \"{query}\" on JNode from JSON of size {len} into took {ConvertTicks(mean)} +/- {ConvertTicks(sd)} ms over {num_query_trials} trials");
                var query_times_str = new string[query_times.Length];
                for (int ii = 0; ii < query_times.Length; ii++)
                {
                    query_times_str[ii] = Math.Round(query_times[ii] / 1e4, 3).ToString(JNode.DOT_DECIMAL_SEP);
                }
                Npp.AddLine($"Query times (ms): {String.Join(", ", query_times_str)}");
                string result_preview = result.ToString().Slice(":300") + "\n...";
                Npp.AddLine($"Preview of result: {result_preview}");
            }
        }

        public static void BenchmarkJNodeToString(int num_trials, string fname)
        {
            // setup
            JsonParser jsonParser = new JsonParser();
            Stopwatch watch = new Stopwatch();
            if (!File.Exists(fname))
            {
                Npp.AddLine($"Can't run benchmark tests because file {fname}\ndoes not exist");
                return;
            }
            string jsonstr = File.ReadAllText(fname);
            int len = jsonstr.Length;
            JNode json = new JNode();
            try
            {
                json = jsonParser.Parse(jsonstr);
            }
            catch (Exception ex)
            {
                Npp.AddLine($"Couldn't run benchmark tests because parsing error occurred:\n{ex}");
                return;
            }
            string json_preview = json.ToString().Slice(":300") + "\n...";
            Npp.AddLine($"Preview of json: {json_preview}");
            // compression benchmark
            long[] toString_times = new long[num_trials];
            for (int ii = 0; ii < num_trials; ii++)
            {
                watch.Reset();
                watch.Start();
                json.ToString(key_value_sep: ":", item_sep: ",");
                watch.Stop();
                long t = watch.Elapsed.Ticks;
                toString_times[ii] = t;
            }
            (double mean, double sd) = GetMeanAndSd(toString_times);
            Npp.AddLine($"To compress JNode from JSON string of {len} took {ConvertTicks(mean)} +/- {ConvertTicks(sd)} " +
                $"ms over {toString_times.Length} trials (minimal whitespace, sort_keys=TRUE)");
            // compression, but with sort_keys=False
            long[] toString_noSort_times = new long[num_trials];
            for (int ii = 0; ii < num_trials; ii++)
            {
                watch.Reset();
                watch.Start();
                json.ToString(sort_keys:false, key_value_sep: ":", item_sep: ",");
                watch.Stop();
                long t = watch.Elapsed.Ticks;
                toString_noSort_times[ii] = t;
            }
            (mean, sd) = GetMeanAndSd(toString_noSort_times);
            Npp.AddLine($"To compress JNode from JSON string of {len} took {ConvertTicks(mean)} +/- {ConvertTicks(sd)} " +
                $"ms over {toString_times.Length} trials (minimal whitespace, sort_keys=FALSE)");
            // pretty-print benchmark
            foreach (var style in new[] {PrettyPrintStyle.Google, PrettyPrintStyle.Whitesmith, PrettyPrintStyle.PPrint})
            {
                long[] prettyPrint_times = new long[num_trials];
                for (int ii = 0; ii < num_trials; ii++)
                {
                    watch.Reset();
                    watch.Start();
                    json.PrettyPrint(style:style);
                    watch.Stop();
                    long t = watch.Elapsed.Ticks;
                    prettyPrint_times[ii] = t;
                }
                // display loading results
                (mean, sd) = GetMeanAndSd(prettyPrint_times);
                Npp.AddLine($"To {style}-style pretty-print JNode from JSON string of {len} took {ConvertTicks(mean)} +/- {ConvertTicks(sd)} " +
                    $"ms over {prettyPrint_times.Length} trials (sort_keys=true, indent=4)");
            }
        }

        public static void BenchmarkRandomJsonAndSchemaValidation(int num_trials)
        {
            var parser = new JsonParser();
            var tweetSchema = (JObject)parser.Parse(File.ReadAllText(@"plugins\JsonTools\testfiles\tweet_schema.json"));
            // restrict to exactly 15 tweets for consistency and 3 items per other array for consistency 
            int num_tweets = 15;
            tweetSchema["minItems"] = new JNode((long)num_tweets, Dtype.INT, 0);
            tweetSchema["maxItems"] = new JNode((long)num_tweets, Dtype.INT, 0);
            long[] make_random_times = new long[num_trials];
            long[] validate_times = new long[num_trials];
            long[] compile_times = new long[num_trials];
            JNode randomTweets = new JNode();
            var watch = new Stopwatch();
            for (int ii = 0; ii < num_trials; ii++)
            {
                watch.Reset();
                watch.Start();
                try
                {
                    randomTweets = RandomJsonFromSchema.RandomJson(tweetSchema, 3, 3, true);
                }
                catch (Exception ex)
                {
                    Npp.AddLine($"While trying to create random tweets from tweet schema, got exception:\r\n{ex}");
                    break;
                }
                watch.Stop();
                make_random_times[ii] = watch.ElapsedTicks;
                watch.Reset();
                watch.Start();
                // validation will succeed because the tweets are generated from that schema
                Func<JNode, JsonSchemaValidator.ValidationProblem?> validator;
                try
                {
                    validator = JsonSchemaValidator.CompileValidationFunc(tweetSchema);
                }
                catch (Exception ex)
                {
                    Npp.AddLine($"While trying to compile tweet schema to validation function, got exception:\r\n{ex}");
                    break;
                }
                watch.Stop();
                compile_times[ii] = watch.ElapsedTicks;
                watch.Reset();
                watch.Start();
                try
                {
                    var vp = validator(randomTweets);
                    if (vp != null)
                    {
                        Npp.AddLine("BAD! JsonSchemaValidator found that tweets made from the tweet schema did not adhere to the tweet schema. Got validation problem\r\n" + vp?.ToString());
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Npp.AddLine($"While trying to validate random tweets under tweet schema, got exception:\r\n{ex}");
                    break;
                }
                watch.Stop();
                validate_times[ii] = watch.ElapsedTicks;
            }
            var len = randomTweets.ToString().Length;
            (double mean, double sd) = GetMeanAndSd(make_random_times);
            Npp.AddLine($"To create a random set of tweet JSON of size {len} ({num_tweets} tweets) based on the matching schema took {ConvertTicks(mean)} +/- {ConvertTicks(sd)} ms over {num_trials} trials");
            (mean, sd) = GetMeanAndSd(compile_times);
            Npp.AddLine($"To compile the tweet schema to a validation function took {ConvertTicks(mean)} +/- {ConvertTicks(sd)} ms over {num_trials} trials");
            (mean, sd) = GetMeanAndSd(validate_times);
            Npp.AddLine($"To validate tweet JSON of size {len} ({num_tweets} tweets) based on the compiled schema took {ConvertTicks(mean)} +/- {ConvertTicks(sd)} ms over {num_trials} trials");
        }


        public static (double mean, double sd) GetMeanAndSd(long[] times)
        {
            double mean = 0;
            foreach (long t in times) { mean += t; }
            mean /= times.Length;
            double sd = 0;
            foreach (long t in times)
            {
                double diff = t - mean;
                sd += diff * diff;
            }
            sd = Math.Sqrt(sd / times.Length);
            return (mean, sd);
        }

        public static double ConvertTicks(double ticks, string new_unit = "ms", int sigfigs = 3)
        {
            switch (new_unit)
            {
                case "ms": return Math.Round(ticks / 1e4, sigfigs);
                case "s": return Math.Round(ticks / 1e7, sigfigs);
                case "ns": return Math.Round(ticks * 100, sigfigs);
                case "mus": return Math.Round(ticks / 10, sigfigs);
                default: throw new ArgumentException("Time unit must be s, mus, ms, or ns");
            }
        }

        //public static void BenchmarkIgnoreCaseStringComparisons(int num_trials, int array_len)
        //{
        //    var watch = new Stopwatch();
        //    var random = new Random(42);
        //    string[] makeRandomStrings()
        //    {
        //        var strings = new string[array_len];
        //        for (int ii = 0; ii < array_len; ii++)
        //        {
        //            StringBuilder sb = new StringBuilder();
        //            var stringlen = random.Next(1, 7);
        //            for (int s = 0; s < stringlen; s++)
        //            {
        //                sb.Append("abcdefghijklmnopqrstuvwxyz"[random.Next(0, 26)]);
        //            }
        //            strings[ii] = sb.ToString();
        //        }
        //        return strings;
        //    }
        //    // the old slow algorithm I used to use for sorting object keys
        //    var toLowerTimes = new long[num_trials];
        //    for (int ii = 0; ii < num_trials; ii++)
        //    {
        //        var strings = makeRandomStrings();
        //        watch.Reset();
        //        watch.Start();
        //        Array.Sort(strings, (x, y) => x.ToLower().CompareTo(y.ToLower()));
        //        watch.Stop();
        //        toLowerTimes[ii] = watch.ElapsedTicks;
        //    }
        //    (double mean, double sd) = GetMeanAndSd(toLowerTimes);
        //    Npp.AddLine($"Sorting {array_len} strings with (x, y) => x.ToLower().CompareTo(y.ToLower()) took "
        //        + ConvertTicks(mean, "mus") + " +/- " + ConvertTicks(sd, "mus") + $" microseconds over {num_trials} trials");
        //    // the fast algorithm that ignores cultural considerations
        //    var ordinalTimes = new long[num_trials];
        //    random = new Random(42);
        //    for (int ii = 0; ii < num_trials; ii++)
        //    {
        //        var strings = makeRandomStrings();
        //        watch.Reset();
        //        watch.Start();
        //        Array.Sort(strings, StringComparer.OrdinalIgnoreCase);
        //        watch.Stop();
        //        ordinalTimes[ii] = watch.ElapsedTicks;
        //    }
        //    (mean, sd) = GetMeanAndSd(ordinalTimes);
        //    Npp.AddLine($"Sorting {array_len} strings with StringComparer.OrdinalIgnoreCase took "
        //        + ConvertTicks(mean, "mus") + " +/- " + ConvertTicks(sd, "mus") + $" microseconds over {num_trials} trials");
        //    // another candidate, InvariantCultureIgnoreCase
        //    var invariantCultureTimes = new long[num_trials];
        //    random = new Random(42);
        //    for (int ii = 0; ii < num_trials; ii++)
        //    {
        //        var strings = makeRandomStrings();
        //        watch.Reset();
        //        watch.Start();
        //        Array.Sort(strings, StringComparer.InvariantCultureIgnoreCase);
        //        watch.Stop();
        //        invariantCultureTimes[ii] = watch.ElapsedTicks;
        //    }
        //    (mean, sd) = GetMeanAndSd(invariantCultureTimes);
        //    Npp.AddLine($"Sorting {array_len} strings with StringComparer.InvariantCultureIgnoreCase) took "
        //        + ConvertTicks(mean, "mus") + " +/- " + ConvertTicks(sd, "mus") + $" microseconds over {num_trials} trials");
        //    // the algorithm I currently use, CurrentCultureIgnoreCase
        //    var currentCultureTimes = new long[num_trials];
        //    random = new Random(42);
        //    for (int ii = 0; ii < num_trials; ii++)
        //    {
        //        var strings = makeRandomStrings();
        //        watch.Reset();
        //        watch.Start();
        //        Array.Sort(strings, StringComparer.CurrentCultureIgnoreCase);
        //        watch.Stop();
        //        currentCultureTimes[ii] = watch.ElapsedTicks;
        //    }
        //    (mean, sd) = GetMeanAndSd(currentCultureTimes);
        //    Npp.AddLine($"Sorting {array_len} strings with StringComparer.CurrentCultureIgnoreCase) took "
        //        + ConvertTicks(mean, "mus") + " +/- " + ConvertTicks(sd, "mus") + $" microseconds over {num_trials} trials");
        //}
    }
}
