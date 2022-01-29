﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.Exceptions;
using TesseractOCR.Iterators;
using Page = TesseractOCR.Page;

// ReSharper disable UnusedMember.Global

namespace Tesseract.Tests
{
    [TestClass]
    public class EngineTests : TesseractTestBase
    {
        private const string TestImagePath = "Ocr/phototest.tif";
        private const string TestImageFileColumn = "Ocr/ocr-five-column.jpg";

        [TestMethod]
        public void CanParseMultiPageTif()
        {
            using (var engine = CreateEngine())
            {
                using (var pixA = TesseractOCR.Pix.Array.LoadMultiPageTiffFromFile(TestFilePath("./processing/multi-page.tif")))
                {
                    var i = 1;
                    foreach (var pix in pixA)
                    {
                        using (var page = engine.Process(pix))
                        {
                            var text = page.Text.Trim();

                            var expectedText = $"Page {i}";
                            Assert.AreEqual(text, expectedText);
                        }

                        i++;
                    }
                }
            }
        }

        [DataTestMethod]
        [DataRow(PageSegMode.SingleBlock, "This is a lot of 12 point text to test the\nocr code and see if it works on all types\nof file format.")]
        [DataRow(PageSegMode.SingleColumn, "This is a lot of 12 point text to test the")]
        [DataRow(PageSegMode.SingleLine, "This is a lot of 12 point text to test the")]
        [DataRow(PageSegMode.SingleWord, "This")]
        [DataRow(PageSegMode.SingleChar, "T")]
        //[DataRow(PageSegMode.SingleBlockVertText, "A line of text", Ignore = "Vertical data missing")]
        public void CanParseText_UsingMode(PageSegMode mode, string expectedText)
        {
            using (var engine = CreateEngine(mode: EngineMode.TesseractAndLstm))
            {
                var demoFilename = $"./Ocr/PSM_{mode}.png";
                using (var pix = LoadTestPix(demoFilename))
                using (var page = engine.Process(pix, mode))
                {
                    var text = page.Text.Trim();
                    Assert.AreEqual(text, expectedText);
                }
            }
        }

        [TestMethod]
        public void CanParseText()
        {
            using (var engine = CreateEngine())
            {
                using (var img = LoadTestPix(TestImagePath))
                {
                    using (var page = engine.Process(img))
                    {
                        var text = page.Text;

                        const string expectedText =
                            "This is a lot of 12 point text to test the\nocr code and see if it works on all types\nof file format.\n\nThe quick brown dog jumped over the\nlazy fox. The quick brown dog jumped\nover the lazy fox. The quick brown dog\njumped over the lazy fox. The quick\nbrown dog jumped over the lazy fox.\n";

                        Assert.AreEqual(text, expectedText);
                    }
                }
            }
        }

        [TestMethod]
        public void CanParseUznFile()
        {
            using (var engine = CreateEngine())
            {
                var inputFilename = TestFilePath(@"Ocr\uzn-test.png");
                using (var img = TesseractOCR.Pix.Image.LoadFromFile(inputFilename))
                {
                    using (var page = engine.Process(img, PageSegMode.AutoOnly))
                    {
                        var text = page.Text;

                        const string expectedText =
                            "This is another test\n";

                        Assert.IsTrue(text.Contains(expectedText));
                    }
                }
            }
        }


        [TestMethod]
        public void CanProcessSpecifiedRegionInImage()
        {
            using (var engine = CreateEngine(mode: EngineMode.LstmOnly))
            {
                using (var img = LoadTestPix(TestImagePath))
                {
                    // See other tests about this bug on coords 0,0
                    using (var page = engine.Process(img, Rect.FromCoords(1, 1, img.Width, 188)))
                    {
                        var region1Text = page.Text;

                        const string expectedTextRegion1 =
                            "This is a lot of 12 point text to test the\nocr code and see if it works on all types\nof file format.\n";

                        Assert.AreEqual(region1Text, expectedTextRegion1);
                    }
                }
            }
        }

        /// <summary>
        ///     Tesseract seems to have a bug processing a region from 0,0, but if you set it to 1,1 things work again. Not sure
        ///     why this is.
        /// </summary>
        [TestMethod]
        public void CanProcessDifferentRegionsInSameImage()
        {
            using (var engine = CreateEngine())
            {
                using (var img = LoadTestPix(TestImagePath))
                {
                    using (var page = engine.Process(img, Rect.FromCoords(1, 1, img.Width, 188)))
                    {
                        var region1Text = page.Text;

                        const string expectedTextRegion1 = "This is a lot of 12 point text to test the\nocr code and see if it works on all types\nof file format.\n";

                        Assert.AreEqual(region1Text, expectedTextRegion1);

                        page.RegionOfInterest = Rect.FromCoords(0, 188, img.Width, img.Height);

                        var region2Text = page.Text;
                        const string expectedTextRegion2 = "The quick brown dog jumped over the\nlazy fox. The quick brown dog jumped\nover the lazy fox. The quick brown dog\njumped over the lazy fox. The quick\nbrown dog jumped over the lazy fox.\n";

                        Assert.AreEqual(region2Text, expectedTextRegion2);
                    }
                }
            }
        }

        [TestMethod]
        public void CanGetSegmentedRegions()
        {
            var expectedCount = 8; // number of text lines in test image

            using (var engine = CreateEngine())
            {
                var imgPath = TestFilePath(TestImagePath);
                using (var img = TesseractOCR.Pix.Image.LoadFromFile(imgPath))
                {
                    using (var page = engine.Process(img))
                    {
                        var boxes = page.GetSegmentedRegions(PageIteratorLevel.TextLine);

                        for (var i = 0; i < boxes.Count; i++)
                        {
                            var box = boxes[i];
                            Console.WriteLine("Box[{0}]: x={1}, y={2}, w={3}, h={4}", i, box.X, box.Y, box.Width,
                                box.Height);
                        }

                        Assert.AreEqual(boxes.Count, expectedCount);
                    }
                }
            }
        }

        [TestMethod]
        public void CanProcessEmptyPixUsingResultIterator()
        {
            string actualResult;
            using (var engine = CreateEngine())
            {
                using (var img = LoadTestPix("ocr/empty.png"))
                {
                    using (var page = engine.Process(img))
                    {
                        actualResult = WriteResultsToString(page, false);
                    }
                }
            }

            Assert.AreEqual(actualResult,
                NormalizeNewLine(@"</word></line>
</para>
</block>
"));
        }

        [TestMethod]
        public void CanProcessMultiplePixs()
        {
            using (var engine = CreateEngine())
            {
                for (var i = 0; i < 3; i++)
                    using (var img = LoadTestPix(TestImagePath))
                    {
                        using (var page = engine.Process(img))
                        {
                            var text = page.Text;

                            const string expectedText =
                                "This is a lot of 12 point text to test the\nocr code and see if it works on all types\nof file format.\n\nThe quick brown dog jumped over the\nlazy fox. The quick brown dog jumped\nover the lazy fox. The quick brown dog\njumped over the lazy fox. The quick\nbrown dog jumped over the lazy fox.\n";

                            Assert.AreEqual(text, expectedText);
                        }
                    }
            }
        }

        [TestMethod]
        public void CanProcessPixUsingResultIterator()
        {
            const string resultPath = @"EngineTests\CanProcessPixUsingResultIterator.txt";

            string actualResult;
            using (var engine = CreateEngine())
            {
                using (var img = LoadTestPix(TestImageFileColumn))
                {
                    using (var page = engine.Process(img))
                    {
                        foreach (var block in page.Blocks)
                        {
                            Debug.Print($"Block text: {block.Text}");
                            Debug.Print($"Block confidence: {block.Confidence}");

                            foreach (var paragraph in block.Paragraphs)
                            {
                                Debug.Print($"Paragraph text: {paragraph.Text}");
                                Debug.Print($"Paragraph confidence: {paragraph.Confidence}");

                                foreach (var textLine in paragraph.TextLines)
                                {
                                    Debug.Print($"Text line text: {textLine.Text}");
                                    Debug.Print($"Text line confidence: {textLine.Confidence}");

                                    foreach (var word in textLine.Words)
                                    {
                                        Debug.Print($"Word text: {word.Text}");
                                        Debug.Print($"Word confidence: {word.Confidence}");
                                        Debug.Print($"Word is from dictionary: {word.IsFromDictionary}");
                                        Debug.Print($"Word is numeric: {word.IsNumeric}");
                                        Debug.Print($"Word language: {word.Language}");

                                        //foreach (var symbol in word.Symbols)
                                        //{
                                        //    Debug.Print($"Symbol text: {symbol.Text}");
                                        //    Debug.Print($"Symbol confidence: {symbol.Confidence}");
                                        //    Debug.Print($"Symbol is superscript: {symbol.IsSuperscript}");
                                        //    Debug.Print($"Symbol is dropcap: {symbol.IsDropcap}");
                                        //}
                                    }
                                }
                            }
                        }

                        actualResult = NormalizeNewLine(WriteResultsToString(page, false));
                    }
                }
            }

            var expectedResultPath = TestResultPath(resultPath);
            var expectedResult = NormalizeNewLine(File.ReadAllText(expectedResultPath));
            
            if (expectedResult == actualResult) return;

            var actualResultPath = TestResultRunFile(resultPath);
            
            Assert.Fail("Expected results to be \"{0}\" but was \"{1}\".", expectedResultPath, actualResultPath);
        }
        
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void CanGenerateHOcrOutput(bool useXHtml)
        {
            string actualResult;
            using (var engine = CreateEngine())
            {
                using (var img = LoadTestPix(TestImagePath))
                {
                    using (var page = engine.Process(img))
                    {
                        actualResult = NormalizeNewLine(page.HOcrText(useXHtml));
                    }
                }
            }

            var resultFilename = $"EngineTests/CanGenerateHOCROutput_{useXHtml}.txt";
            var expectedFilename = TestResultPath(resultFilename);

            if (File.Exists(expectedFilename))
            {
                var expectedResult = NormalizeNewLine(File.ReadAllText(expectedFilename));
                if (expectedResult != actualResult)
                {
                    var actualFilename = TestResultRunFile(resultFilename);
                    Assert.Fail("Expected results to be {0} but was {1}", expectedFilename, actualFilename);
                }
            }
            else
            {
                var actualFilename = TestResultRunFile(resultFilename);
                Assert.Fail("Expected result did not exist, actual results saved to {0}", actualFilename);
            }
        }

        [TestMethod]
        public void CanGenerateAltoOutput()
        {
            string actualResult;
            using (var engine = CreateEngine())
            {
                using (var img = LoadTestPix(TestImagePath))
                {
                    using (var page = engine.Process(img))
                    {
                        actualResult = NormalizeNewLine(page.AltoText);
                    }
                }
            }

            var resultFilename = "EngineTests/CanGenerateAltoOutput.txt";
            var expectedFilename = TestResultPath(resultFilename);
            if (File.Exists(expectedFilename))
            {
                var expectedResult = NormalizeNewLine(File.ReadAllText(expectedFilename));
                if (expectedResult != actualResult)
                {
                    var actualFilename = TestResultRunFile(resultFilename);
                    //File.WriteAllText(actualFilename,actualResult);
                    Assert.Fail("Expected results to be {0} but was {1}", expectedFilename, actualFilename);
                }
            }
            else
            {
                var actualFilename = TestResultRunFile(resultFilename);
                //File.WriteAllText(actualFilename,actualResult);
                Assert.Fail("Expected result did not exist, actual results saved to {0}", actualFilename);
            }
        }

        [TestMethod]
        public void CanGenerateTsvOutput()
        {
            string actualResult;
            using (var engine = CreateEngine())
            {
                using (var img = LoadTestPix(TestImagePath))
                {
                    using (var page = engine.Process(img))
                    {
                        actualResult = NormalizeNewLine(page.TsvText);
                    }
                }
            }

            var resultFilename = "EngineTests/CanGenerateTsvOutput.txt";
            var expectedFilename = TestResultPath(resultFilename);
            if (File.Exists(expectedFilename))
            {
                var expectedResult = NormalizeNewLine(File.ReadAllText(expectedFilename));
                if (expectedResult != actualResult)
                {
                    var actualFilename = TestResultRunFile(resultFilename);
                    //File.WriteAllText(actualFilename,actualResult);
                    Assert.Fail("Expected results to be {0} but was {1}", expectedFilename, actualFilename);
                }
            }
            else
            {
                var actualFilename = TestResultRunFile(resultFilename);
                //File.WriteAllText(actualFilename,actualResult);
                Assert.Fail("Expected result did not exist, actual results saved to {0}", actualFilename);
            }
        }

        [TestMethod]
        public void CanGenerateBoxOutput()
        {
            string actualResult;
            using (var engine = CreateEngine())
            {
                using (var img = LoadTestPix(TestImagePath))
                {
                    using (var page = engine.Process(img))
                    {
                        actualResult = NormalizeNewLine(page.BoxText);
                    }
                }
            }

            var resultFilename = "EngineTests/CanGenerateBoxOutput.txt";
            var expectedFilename = TestResultPath(resultFilename);

            if (File.Exists(expectedFilename))
            {
                var expectedResult = NormalizeNewLine(File.ReadAllText(expectedFilename));
                if (expectedResult != actualResult)
                {
                    var actualFilename = TestResultRunFile(resultFilename);
                    Assert.Fail("Expected results to be {0} but was {1}", expectedFilename, actualFilename);
                }
            }
            else
            {
                var actualFilename = TestResultRunFile(resultFilename);
                Assert.Fail("Expected result did not exist, actual results saved to {0}", actualFilename);
            }
        }

        [TestMethod]
        public void CanGenerateLSTMBoxOutput()
        {
            string actualResult;
            using (var engine = CreateEngine())
            {
                using (var img = LoadTestPix(TestImagePath))
                {
                    using (var page = engine.Process(img))
                    {
                        actualResult = NormalizeNewLine(page.LstmBoxText);
                    }
                }
            }

            var resultFilename = "EngineTests/CanGenerateLSTMBoxOutput.txt";
            var expectedFilename = TestResultPath(resultFilename);
            if (File.Exists(expectedFilename))
            {
                var expectedResult = NormalizeNewLine(File.ReadAllText(expectedFilename));
                if (expectedResult != actualResult)
                {
                    var actualFilename = TestResultRunFile(resultFilename);
                    //File.WriteAllText(actualFilename,actualResult);
                    Assert.Fail("Expected results to be {0} but was {1}", expectedFilename, actualFilename);
                }
            }
            else
            {
                var actualFilename = TestResultRunFile(resultFilename);
                //File.WriteAllText(actualFilename,actualResult);
                Assert.Fail("Expected result did not exist, actual results saved to {0}", actualFilename);
            }
        }

        [TestMethod]
        public void CanGenerateWordStrBoxOutput()
        {
            string actualResult;
            using (var engine = CreateEngine())
            {
                using (var img = LoadTestPix(TestImagePath))
                {
                    using (var page = engine.Process(img))
                    {
                        actualResult = NormalizeNewLine(page.WordStrBoxText);
                    }
                }
            }

            var resultFilename = "EngineTests/CanGenerateWordStrBoxOutput.txt";
            var expectedFilename = TestResultPath(resultFilename);
            if (File.Exists(expectedFilename))
            {
                var expectedResult = NormalizeNewLine(File.ReadAllText(expectedFilename));
                if (expectedResult != actualResult)
                {
                    var actualFilename = TestResultRunFile(resultFilename);
                    //File.WriteAllText(actualFilename,actualResult);
                    Assert.Fail("Expected results to be {0} but was {1}", expectedFilename, actualFilename);
                }
            }
            else
            {
                var actualFilename = TestResultRunFile(resultFilename);
                //File.WriteAllText(actualFilename,actualResult);
                Assert.Fail("Expected result did not exist, actual results saved to {0}", actualFilename);
            }
        }

        [TestMethod]
        public void CanGenerateUNLVOutput()
        {
            string actualResult;
            using (var engine = CreateEngine())
            {
                using (var img = LoadTestPix(TestImagePath))
                {
                    using (var page = engine.Process(img))
                    {
                        actualResult = NormalizeNewLine(page.UnlvText);
                    }
                }
            }

            const string resultFilename = "EngineTests/CanGenerateUNLVOutput.txt";
            var expectedFilename = TestResultPath(resultFilename);
            if (File.Exists(expectedFilename))
            {
                var expectedResult = NormalizeNewLine(File.ReadAllText(expectedFilename));
                if (expectedResult == actualResult) return;
                var actualFilename = TestResultRunFile(resultFilename);
                //File.WriteAllText(actualFilename,actualResult);
                Assert.Fail("Expected results to be {0} but was {1}", expectedFilename, actualFilename);
            }
            else
            {
                var actualFilename = TestResultRunFile(resultFilename);
                //File.WriteAllText(actualFilename,actualResult);
                Assert.Fail("Expected result did not exist, actual results saved to {0}", actualFilename);
            }
        }

        [TestMethod]
        public void CanProcessPixUsingResultIteratorAndChoiceIterator()
        {
            string actualResult;
            
            using (var engine = CreateEngine())
            {
                using (var img = LoadTestPix(TestImagePath))
                {
                    using (var page = engine.Process(img))
                    {
                        actualResult = WriteResultsToString(page, true);
                    }
                }
            }

            const string resultFilename = @"EngineTests\CanProcessPixUsingResultIteratorAndChoiceIterator.txt";
            
            var expectedResultFilename = TestResultPath(resultFilename);
            var expectedResult = NormalizeNewLine(File.ReadAllText(expectedResultFilename));

            if (expectedResult != actualResult)
            {
                var actualResultPath = TestResultRunFile(resultFilename);
                Assert.Fail("Expected results to be {0} but was {1}", expectedResultFilename, actualResultPath);
            }
        }

        [TestMethod]
        public void Initialise_CanLoadConfigFile()
        {
            using (var engine = new Engine(DataPath, "eng", EngineMode.Default, "bazzar"))
            {
                // verify that the config file was loaded
                if (engine.TryGetStringVariable("user_words_suffix", out var userPatternsSuffix))
                    Assert.AreEqual(userPatternsSuffix, "user-words");
                else
                    Assert.Fail("Failed to retrieve value for 'user_words_suffix'.");

                using (var img = LoadTestPix(TestImagePath))
                {
                    using (var page = engine.Process(img))
                    {
                        var text = page.Text;

                        const string expectedText =
                            "This is a lot of 12 point text to test the\nocr code and see if it works on all types\nof file format.\n\nThe quick brown dog jumped over the\nlazy fox. The quick brown dog jumped\nover the lazy fox. The quick brown dog\njumped over the lazy fox. The quick\nbrown dog jumped over the lazy fox.\n";
                        Assert.AreEqual(text, expectedText);
                    }
                }
            }
        }

        [TestMethod]
        public void Initialise_CanPassInitVariables()
        {
            var initVars = new Dictionary<string, object>
            {
                { "load_system_dawg", false }
            };
            using (var engine = new Engine(DataPath, "eng", EngineMode.Default, Enumerable.Empty<string>(),
                       initVars, false))
            {
                if (!engine.TryGetBoolVariable("load_system_dawg", out var loadSystemDawg))
                    Assert.Fail("Failed to get 'load_system_dawg'.");
                Assert.IsFalse(loadSystemDawg);
            }
        }

        [Ignore("Missing russian language data")]
        public static void Initialise_Rus_ShouldStartEngine()
        {
            using (new Engine(DataPath, "rus", EngineMode.Default))
            {
            }
        }

        [TestMethod]
        public void Initialize_ShouldStartEngine()
        {
            const string dataPath = "tessdata";

            using (new Engine(dataPath, "eng", EngineMode.Default))
            {
            }
        }

        [TestMethod]
        [ExpectedException(typeof(TesseractException))]
        public void Initialize_ShouldThrowErrorIfDatapathNotCorrect()
        {
            using (new Engine(AbsolutePath(@"./IDontExist"), "eng", EngineMode.Default))
            {
            }
        }

        private static void WriteResultToString(StringBuilder output, Result iterator)
        {
            var tag = string.Empty;

            switch (iterator.Element)
            {
                case PageIteratorLevel.Block:
                    tag = "block";
                    break;

                case PageIteratorLevel.Paragraph:
                    tag = "para";
                    break;

                case PageIteratorLevel.TextLine:
                    tag = "line";
                    break;

                case PageIteratorLevel.Word:
                    tag = "word";
                    break;

                case PageIteratorLevel.Symbol:
                    tag = "symbol";
                    break;
            }

            if (iterator.IsAtBeginning)
            {
                var confidence = iterator.Confidence / 100;
                var bounds = iterator.BoundingBox;
                if (bounds.HasValue)
                    output.AppendFormat(CultureInfo.InvariantCulture, "<{0} confidence=\"{1:P}\" bounds=\"{2}, {3}, {4}, {5}\">", tag,
                        confidence, bounds.Value.X1, bounds.Value.Y1, bounds.Value.X2, bounds.Value.Y2);
                else
                    output.AppendFormat(CultureInfo.InvariantCulture, "<{0} confidence=\"{1:P}\">", tag, confidence);

            }
            else 
                output.AppendFormat("</{0}>", tag);

            output.AppendLine();
        }

        private static string WriteResultsToString(Page page, bool outputChoices)
        {
            var output = new StringBuilder();
            using (var iterator = page.ResultIterator)
            {
                iterator.Begin();
                do
                {
                    do
                    {
                        do
                        {
                            do
                            {
                                do
                                {
                                    WriteResultToString(output, iterator);

                                    // Symbol and choices
                                    //if (outputChoices)
                                    //    using (var choiceIterator = iterator.ChoiceIterator)
                                    //    {
                                    //        var symbolConfidence = iterator.Confidence;
                                    //        if (choiceIterator != null)
                                    //        {
                                    //            output.AppendFormat(CultureInfo.InvariantCulture, "<symbol text=\"{0}\" confidence=\"{1:P}\">", iterator.Text, symbolConfidence);
                                    //            output.Append("<choices>");
                                                
                                    //            do
                                    //            {
                                    //                var choiceConfidence = choiceIterator.Confidence / 100;
                                    //                output.AppendFormat(CultureInfo.InvariantCulture, "<choice text=\"{0}\" confidence\"{1:P}\"/>", choiceIterator.Text, choiceConfidence);
                                    //            } 
                                    //            while (choiceIterator.Next());

                                    //            output.Append("</choices>");
                                    //            output.Append("</symbol>");
                                    //        }
                                    //        else
                                    //            output.AppendFormat(CultureInfo.InvariantCulture, "<symbol text=\"{0}\" confidence=\"{1:P}\"/>", iterator.Text, symbolConfidence);
                                    //    }
                                    //else
                                    //    output.Append(iterator.Text);

                                } 
                                while (iterator.NextElement()); // Symbol

                            } 
                            while (iterator.NextElement()); // Word
                        } 
                        while (iterator.NextElement()); // Text line
                    } 
                    while (iterator.NextElement()); // Paragraph
                } 
                while (iterator.NextLevel(PageIteratorLevel.Block));
            }

            return NormalizeNewLine(output.ToString());
        }

        #region Variable set\get
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void CanSetBooleanVariable(bool variableValue)
        {
            const string variableName = "classify_enable_learning";
            using (var engine = CreateEngine())
            {
                var variableWasSet = engine.SetVariable(variableName, variableValue);
                Assert.IsTrue(variableWasSet, "Failed to set variable '{0}'.", variableName);
                
                if (engine.TryGetBoolVariable(variableName, out var result))
                    Assert.AreEqual(result, variableValue);
                else
                    Assert.Fail("Failed to retrieve value for '{0}'.", variableName);
            }
        }

        /// <summary>
        ///     As per Bug #52 setting 'classify_bln_numeric_mode' variable to '1' causes the engine to fail on processing.
        /// </summary>
        [TestMethod]
        public void CanSetClassifyBlnNumericModeVariable()
        {
            using (var engine = CreateEngine())
            {
                engine.SetVariable("classify_bln_numeric_mode", 1);

                using (var img = TesseractOCR.Pix.Image.LoadFromFile(TestFilePath("./processing/numbers.png")))
                {
                    using (var page = engine.Process(img))
                    {
                        var text = page.Text;

                        const string expectedText = "1234567890\n";

                        Assert.AreEqual(text, expectedText);
                    }
                }
            }
        }

        [DataTestMethod]
        [DataRow("edges_boxarea", 0.875)]
        [DataRow("edges_boxarea", 0.9)]
        [DataRow("edges_boxarea", -0.9)]
        public void CanSetDoubleVariable(string variableName, double variableValue)
        {
            using (var engine = CreateEngine())
            {
                var variableWasSet = engine.SetVariable(variableName, variableValue);
                Assert.IsTrue(variableWasSet, "Failed to set variable '{0}'.", variableName);
                
                if (engine.TryGetDoubleVariable(variableName, out var result))
                    Assert.AreEqual(result, variableValue);
                else
                    Assert.Fail("Failed to retrieve value for '{0}'.", variableName);
            }
        }

        [DataTestMethod]
        [DataRow("edges_children_count_limit", 45)]
        [DataRow("edges_children_count_limit", 20)]
        [DataRow("textord_testregion_left", 20)]
        [DataRow("textord_testregion_left", -20)]
        public void CanSetIntegerVariable(string variableName, int variableValue)
        {
            using (var engine = CreateEngine())
            {
                var variableWasSet = engine.SetVariable(variableName, variableValue);
                Assert.IsTrue(variableWasSet, "Failed to set variable '{0}'.", variableName);
                
                if (engine.TryGetIntVariable(variableName, out var result))
                    Assert.AreEqual(result, variableValue);
                else
                    Assert.Fail("Failed to retrieve value for '{0}'.", variableName);
            }
        }

        [DataTestMethod]
        [DataRow("tessedit_char_whitelist", "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ")]
        [DataRow("tessedit_char_whitelist", "")]
        [DataRow("tessedit_char_whitelist", "Test")]
        [DataRow("tessedit_char_whitelist", "chinese 漢字")] // Issue 68
        public void CanSetStringVariable(string variableName, string variableValue)
        {
            using (var engine = CreateEngine())
            {
                var variableWasSet = engine.SetVariable(variableName, variableValue);
                Assert.IsTrue(variableWasSet, "Failed to set variable '{0}'.", variableName);
                
                if (engine.TryGetStringVariable(variableName, out var result))
                    Assert.AreEqual(result, variableValue);
                else
                    Assert.Fail("Failed to retrieve value for '{0}'.", variableName);
            }
        }

        [TestMethod]
        public void CanGetStringVariableThatDoesNotExist()
        {
            using (var engine = CreateEngine())
            {
                var success = engine.TryGetStringVariable("illegal-variable", out var result);
                Assert.IsFalse(success);
                Assert.IsNull(result);
            }
        }
        #endregion Variable set\get

        #region Variable print
        [TestMethod]
        public void CanPrintVariables()
        {
            const string resultFilename = @"EngineTests\CanPrintVariables.txt";

            using (var engine = CreateEngine())
            {
                var actualResultsFilename = TestResultRunFile(resultFilename);
                Assert.IsTrue(engine.TryPrintVariablesToFile(actualResultsFilename));
                var actualResult = NormalizeNewLine(File.ReadAllText(actualResultsFilename));

                // Load the expected results and verify that they match
                var expectedResultFilename = TestResultPath(resultFilename);
                var expectedResult = NormalizeNewLine(File.ReadAllText(expectedResultFilename));

                if (expectedResult != actualResult)
                    Assert.Fail("Expected results to be \"{0}\" but was \"{1}\".", expectedResultFilename,
                        actualResultsFilename);
            }
        }
        #endregion
    }
}