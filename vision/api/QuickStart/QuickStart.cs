// Copyright(c) 2017 Google Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not
// use this file except in compliance with the License. You may obtain a copy of
// the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
// License for the specific language governing permissions and limitations under
// the License.

// [START vision_quickstart]

using Google.Cloud.Vision.V1;
//using Google.Vision;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using Google.Apis.Auth.OAuth2.Requests;
using Newtonsoft.Json;
using static Google.Cloud.Vision.V1.TextAnnotation.Types.DetectedBreak.Types;

namespace GoogleCloudSamples
{
   public class TextBox
   {
      public int Index;
      public string Description;
      public Rectangle Rect;
      public Point LowerRight;
      public long Size;
      public float Confidence;
      public BoundingPoly Bounds;

      public TextBox()
      {

      }
      /*
      public TextBox(BoundingPoly bp, float confidence, string description, int index)
      {
         Description = description;
         Index = index;
         Confidence = confidence;
         Bounds = bp;
         Debug.Assert(bp.Vertices.Count == 4);
         int l = bp.Vertices.Min(x => x.X);
         int r = bp.Vertices.Max(x => x.X);
         int t = bp.Vertices.Min(x => x.Y);
         int b = bp.Vertices.Max(x => x.Y);
         Rect = new Rectangle(new Point(l, t), new Size(r - l, b - t));
         LowerRight = new Point(r, b);
         Size = Rect.Size.Height * Rect.Size.Width;
      }
      */
      public TextBox(Rectangle r1, float confidence, string description, int index)
      {
         Description = description;
         Index = index;
         Confidence = confidence;
         Rect = r1;
         LowerRight = new Point(r1.Right, r1.Bottom);
         Size = Rect.Size.Height * Rect.Size.Width;
      }

      public override string ToString()
      {
         var d = Description;
         d = d.Substring(0, d.Length > 25 ? 25 : d.Length).Replace("\r\n", @"\n");

         return $"({Index}. {Size} {Rect},{Rect.Right},{Rect.Bottom} [{d}] {Description.Length})";
      }
   }

   public class RectFromVertices
   {
      public static Rectangle Convert(BoundingPoly bp)
      {
         int l = bp.Vertices.Min(x => x.X);
         int r = bp.Vertices.Max(x => x.X);
         int t = bp.Vertices.Min(x => x.Y);
         int b = bp.Vertices.Max(x => x.Y);
         var rect = new Rectangle(new Point(l, t), new Size(r - l, b - t));
         return rect;
      }
      public static Rectangle Expand(Rectangle r1, BoundingPoly bp)
      {
         int l = bp.Vertices.Min(x => x.X);
         int r = bp.Vertices.Max(x => x.X);
         int t = bp.Vertices.Min(x => x.Y);
         int b = bp.Vertices.Max(x => x.Y);
         l = (l > r1.Left) ? r1.Left : l;
         r = (r < r1.Right) ? r1.Right : r;
         t = (t > r1.Top) ? r1.Top : t;
         b = (b < r1.Bottom) ? r1.Bottom : b;
         var rect = new Rectangle(new Point(l, t), new Size(r - l, b - t));
         return rect;


      }

   }
   public class Box
   {
      public int Index;
      [JsonIgnore]
      public EntityAnnotation Annotation;
      public string Description;
      public Rectangle Rect;
      public Point LowerRight;
      public long Size;
      [JsonIgnore]
      public BoundingPoly Bounds;
      [JsonIgnore]
      public Box Parent;
      [JsonIgnore]
      public List<Box> Children = new List<Box>();

      public Box()
      {

      }

      public Box(EntityAnnotation ann, int index)
      {
         BoundingPoly bp = ann.BoundingPoly;
         Annotation = ann;
         Description = ann.Description;
         Index = index;
         Bounds = bp;
         Debug.Assert(bp.Vertices.Count == 4);
         int l = bp.Vertices.Min(x => x.X);
         int r = bp.Vertices.Max(x => x.X);
         int t = bp.Vertices.Min(x => x.Y);
         int b = bp.Vertices.Max(x => x.Y);
         Rect = new Rectangle(new Point(l, t), new Size(r - l, b - t));
         LowerRight = new Point(r, b);
         Size = Rect.Size.Height * Rect.Size.Width;
      }

      public bool FindParents(SortedList<long, Box> boxes)
      {
         foreach (var kvp in boxes.Reverse())
         {
            if (kvp.Key < Size)
               break;
            else if (kvp.Value == this)
               continue;
            if (kvp.Value.Contains(this))
            {
               Parent = kvp.Value;
               foreach (var b1 in kvp.Value.Children)
               {
                  if (b1.Contains(this))
                  {
                     b1.Children.Add(this);
                     if (b1.Size < Parent.Size)
                     {
                        Parent = b1;
                     }
                  }
               }

               break;
            }
         }
         return Parent != null;
      }

      public bool Contains(Box b)
      {
         if (b.Size > Size) return false;
         if (b.Rect.X < Rect.X) return false;
         if (b.Rect.Y < Rect.Y) return false;
         if (b.LowerRight.X > LowerRight.X) return false;
         if (b.LowerRight.Y > LowerRight.Y) return false;
         return true;
      }

      public override string ToString()
      {
         var d = Description;
         d = d.Substring(0, d.Length > 25 ? 25 : d.Length).Replace("\r\n", @"\n");

         return $"({Index}. {Size} {Rect},{Rect.Right},{Rect.Bottom} [{d}] {Description.Length})";
      }
   }


   public class QuickStart
   {
      private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
      /// <summary>
      /// Comparer for comparing two keys, handling equality as beeing greater
      /// Use this Comparer e.g. with SortedLists or SortedDictionaries, that don't allow duplicate keys
      /// </summary>
      /// <typeparam name="TKey"></typeparam>
      public class DuplicateKeyComparer<TKey>
         :
            IComparer<TKey> where TKey : IComparable
      {
         #region IComparer<TKey> Members

         public int Compare(TKey x, TKey y)
         {
            int result = x.CompareTo(y);

            if (result == 0)
               return 1;   // Handle equality as being greater
            else
               return result;
         }

         #endregion
      }
      public static void Main(string[] args)
      {
         foreach (var path in new string[] {  @"D:\Dropbox\danny\Flood\OCR\Mortgage Examples\20191216png" }) //D:\checks", @"D:\billpayment" }) @"D:\Dropbox\danny\Flood\OCR\Mortgage Examples\IRS Check", 
         {/*
            var path = @"F:\Dropbox\Danny\Flood\OCR\Elevation Certificates New\99014600682018\fields\99014600682018_01_fields.jpg";
         path = @"C:\OCR\99014600682018_02_fields_nude.jpg";
         path = @"C:\OCR\99014600682018_02_fields_copy.jpg";
         path = @"C:\OCR\99014600682018_02_fields.png";
         path = @"C:\OCR\99014600682018_02_fields-8bit.png";
         */
            var filesToDo = new List<string>();
            var wildcard = "*.*";
            if (path != null)
            {
               if (File.Exists(path))
               {
                  filesToDo.Add(path);
               }
               else
               {
                  if (Directory.Exists(path))
                  {
                     foreach (var file in Directory.GetFiles(path, wildcard))
                     {
                        var fi = new FileInfo(file);
                        if (fi.Extension == ".jpg" || fi.Extension == ".tif" || fi.Extension == ".png")
                           filesToDo.Add(file);
                     }
                  }
                  else
                  {
                     logger.Error($"invalid pdf/dir 'path' parameter: {path}");
                     return;
                  }
               }
            }
            if (filesToDo.Count > 0)
            {
               foreach (var file in filesToDo)
               {
                  var fi = new FileInfo(file);
                  var stem = Path.GetFileNameWithoutExtension(file);
                  var dirOut = Path.Combine(fi.DirectoryName, stem);
                  Directory.CreateDirectory(dirOut);

                  textMain(fi, dirOut);
               }
            }
            //file = @"C:\OCR\99014600682018_02_fields_paint.png";
            //               fullTextMain(path);

         }
      }

      public static void fullTextMain(string file)
      {
         var textBoxes = new List<TextBox>();
         // Instantiates a client
         var client = ImageAnnotatorClient.Create();
         // Load the image file into memory
         //var file = @"F:\Dropbox\OCR\Single\data\13864584_3_ocr~20181130_page3pdf.png";
         var boxFile = file + ".DetectDocumentText.response.json";
         var allBoxes = new List<Box>();
         var boxesBySize = new SortedList<long, Box>(new DuplicateKeyComparer<long>());
         int num = 0;
         if (!File.Exists(boxFile) || true)
         {
            var image = Image.FromFile(file);

            var response = client.DetectDocumentText(image);
            var serializer = new JsonSerializer { NullValueHandling = NullValueHandling.Ignore };

            using (StreamWriter sw = new StreamWriter(boxFile))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
               writer.Formatting = Newtonsoft.Json.Formatting.Indented;
               serializer.Serialize(writer, response);
               // {"ExpiryDate":new Date(1230375600000),"Price":0}
            }
         }

         var json = File.ReadAllText(boxFile);
         var response2 = JsonConvert.DeserializeObject<TextAnnotation>(json);
         int index = 0;
         float confidence = 1.01f;
         var pageRange = Enumerable.Range(0, response2.Pages.Count);

         foreach (var pnum in pageRange)
         {
            var page = response2.Pages[pnum];
            var blockRange = Enumerable.Range(0, page.Blocks.Count);
            foreach (var bnum in blockRange)
            {
               var block = page.Blocks[bnum];
               logger.Info($"block {bnum} {block.BoundingBox} paras={block.Paragraphs.Count}");
               var paraRange = Enumerable.Range(0, block.Paragraphs.Count);
               foreach (var paraNum in paraRange)
               {
                  var paragraph = block.Paragraphs[paraNum];
                  logger.Info($"{paragraph.BoundingBox} {paragraph.CalculateSize()} {paragraph.Words.Count}");
                  var sb = new StringBuilder();
                  Rectangle rect = Rectangle.Empty;
                  var line = new StringBuilder();

                  foreach (var word in paragraph.Words)
                  {
                     if (word.Confidence < confidence)
                        confidence = word.Confidence;
                     foreach (var symbol in word.Symbols)
                     {
                        line.Append(symbol.Text);
                        if (rect == Rectangle.Empty)
                        {
                           rect = RectFromVertices.Convert(symbol.BoundingBox);
                        }
                        else rect = RectFromVertices.Expand(rect, symbol.BoundingBox);

                        try
                        {
                           if (symbol.Property?.DetectedBreak != null)
                           {
                              switch (symbol.Property.DetectedBreak.Type)
                              {
                                 case BreakType.Space:
                                    line.Append(" ");
                                    break;
                                 case BreakType.EolSureSpace:
                                 case BreakType.LineBreak:
                                    index = AddTextBox(index, ref confidence, line, textBoxes, ref rect);
                                    break;
                                 default:
                                    line.Append(" ");
                                    break;
                              }
                           }
                        }
                        catch (Exception ex)
                        {
                           logger.Error(ex, $"{index} of {file} {paragraph.ToString()}");
                        }
                     }
                  }
                  if (line.Length > 0)
                     index = AddTextBox(index, ref confidence, line, textBoxes, ref rect);
               }
            }
            var googleResultsFile = file + ".DetectDocumentText.text.json";

            var jsonOut = JsonConvert.SerializeObject(textBoxes, Formatting.Indented);
            File.WriteAllText(googleResultsFile, jsonOut);
         }

#if IGNORE
      foreach (var page in response.Pages)
               foreach( var block)
            {
               num++;
               if (annotation.Description != null)
               {
                  if (annotation.BoundingPoly == null)
                  {
                     logger.Info($"{num}. {annotation.ToString()}");
                  }
                  else if (annotation.BoundingPoly.Vertices.Count == 4)
                  {
                     var b = new Box(annotation, allBoxes.Count);
                     allBoxes.Add(b);
                     logger.Info(
                        $"{num}. {b} cs:{annotation.CalculateSize()} Mid:{annotation.Mid} Parent:{b.Parent?.Index}");
                  }
                  else
                     logger.Info(
                        $"{num}.Vertices: {annotation.BoundingPoly.Vertices.Count} cs:{annotation.CalculateSize()} {annotation.Description}");
               }
               else
               {
                  logger.Info($"{num}. {annotation.ToString()}");
               }
            }
         }
         boxesBySize = new SortedList<long, Box>(new DuplicateKeyComparer<long>());
         var json = File.ReadAllText(boxFile);
         var boxes = JsonConvert.DeserializeObject<List<Box>>(json);
         foreach (var b in boxes)
         {
            num++;
            b.Index = num;
            logger.Info($"{b}");

            boxesBySize.Add(b.Size, b);
         }
         logger.Info("-----------------------------------------------------");
         foreach (var b in boxes)
         {
            b.FindParents(boxesBySize);
            logger.Info(
               $"{b} cs:{b.Annotation.CalculateSize()} Parent:{b.Parent?.Index}");
         }
#endif      
      }
      private static int AddTextBox(int index, ref float confidence, StringBuilder line, List<TextBox> textBoxes, ref Rectangle rect)
      {
         index++;
         var tb = new TextBox(rect, confidence, line.ToString().Trim(), index);
         textBoxes.Add(tb);
         rect = Rectangle.Empty;
         confidence = 0.0f;
         line.Clear();
         return index;
      }

      public static void textMain(FileInfo fi, string dirOut)
      {
            // Instantiates a client
         var client = ImageAnnotatorClient.Create();
         var file = fi.FullName;
         var stem = Path.GetFileNameWithoutExtension(file);

         // Load the image file into memory
         //  var file = @"F:\Dropbox\OCR\Single\data\13864584_3_ocr~20181130_page3pdf.png";
         var detectTextResponse = Path.Combine(dirOut, stem + ".DetectText.raw.google.response.json");
         var boxFile = Path.Combine(dirOut, stem + ".DetectText.interpreted.json");
         try
         {
            var allBoxes = new List<Box>();
            var boxesBySize = new SortedList<long, Box>(new DuplicateKeyComparer<long>());
            int num = 0;
            if (!File.Exists(boxFile) || true)
            {
               var image = Image.FromFile(file);

               //CR\Single\13864584_3.jpg"); //wakeupcat.jpg");
               //var response = client.DetectText(image);

               // Performs label detection on the image file
               /*
               var responseObj = client.DetectLocalizedObjects(image);
               foreach (var localizedObject in responseObj)
               {
                  Console.Write($"\n{localizedObject.Name}");
                  Console.WriteLine($" (confidence: {localizedObject.Score})");
                  Console.WriteLine("Normalized bounding polygon vertices: ");

                  foreach (var vertex
                     in localizedObject.BoundingPoly.NormalizedVertices)
                  {
                     Console.WriteLine($" - ({vertex.X}, {vertex.Y})");
                  }
               }
               */
               var serializer = new JsonSerializer { NullValueHandling = NullValueHandling.Ignore };

               var response = client.DetectText(image);
               using (StreamWriter sw = new StreamWriter(detectTextResponse))
               using (JsonWriter writer = new JsonTextWriter(sw))
               {
                  writer.Formatting = Formatting.Indented;
                  serializer.Serialize(writer, response);
                  // {"ExpiryDate":new Date(1230375600000),"Price":0}
               }

               foreach (var annotation in response)
               {
                  num++;
                  if (annotation.Description != null)
                  {
                     if (annotation.BoundingPoly == null)
                     {
                        logger.Info($"{num}. {annotation.ToString()}");
                     }
                     else if (annotation.BoundingPoly.Vertices.Count == 4)
                     {
                        var b = new Box(annotation, allBoxes.Count);
                        allBoxes.Add(b);
                        logger.Info(
                           $"{num}. {b} cs:{annotation.CalculateSize()} Mid:{annotation.Mid} Parent:{b.Parent?.Index}");
                     }
                     else
                        logger.Info(
                           $"{num}.Vertices: {annotation.BoundingPoly.Vertices.Count} cs:{annotation.CalculateSize()} {annotation.Description}");
                  }
                  else
                  {
                     logger.Info($"{num}. {annotation.ToString()}");
                  }
               }

               using (StreamWriter sw = new StreamWriter(boxFile))
               using (JsonWriter writer = new JsonTextWriter(sw))
               {
                  writer.Formatting = Formatting.Indented;
                  serializer.Serialize(writer, allBoxes);
                  // {"ExpiryDate":new Date(1230375600000),"Price":0}
               }
               var childBoxes = allBoxes.Select(x => x.Children.Count == 0).ToArray();
               logger.Info($"end boxes {childBoxes.Length}");
            }
            /*
            boxesBySize = new SortedList<long, Box>(new DuplicateKeyComparer<long>());
            var json = File.ReadAllText(boxFile);
            var boxes = JsonConvert.DeserializeObject<List<Box>>(json);
            foreach (var b in boxes)
            {
               num++;
               b.Index = num;
               logger.Info($"{b}");

               boxesBySize.Add(b.Size, b);
            }
            logger.Info("-----------------------------------------------------");
            foreach (var b in boxes)
            {
               b.FindParents(boxesBySize);
               logger.Info(
                  $"{b} cs:{b.Annotation.CalculateSize()} Parent:{b.Parent?.Index}");
            }
            */
         }
         catch (Exception ex)
         {
            logger.Error(ex, $"error on {fi.FullName}");
            var errorFile = boxFile + ".error.txt";
            File.WriteAllText(errorFile, ex.ToString());
         }
      }
   }
}
// [END vision_quickstart]
