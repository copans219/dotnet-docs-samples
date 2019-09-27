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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using Google.Apis.Auth.OAuth2.Requests;
using Newtonsoft.Json;

namespace GoogleCloudSamples
{

   public class Box
   {
      public int Index;
      public EntityAnnotation Annotation;
      public string Description;
      public Rectangle Rect;
      public Point LowerRight;
      public long Size;
      public BoundingPoly Bounds;
      public Box Parent;
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

      public 
      foreach (var kvp in boxes)
      {
         if (kvp.Key<Size)
            break;
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
         return $"({Index}. {Size} {Rect},{Rect.Right},{Rect.Bottom} [{Description}])";
      }
   }


   public class QuickStart
   {
      private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
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
         // Instantiates a client
         var client = ImageAnnotatorClient.Create();
         // Load the image file into memory
         var file = @"F:\Dropbox\OCR\Single\data\13864584_3_ocr~20181130_page3pdf.png";
         var boxFile = file + ".boxes";
         var allBoxes = new List<Box>();
         var boxesBySize = new SortedList<long, Box>(new DuplicateKeyComparer<long>());
         int num = 0;
         if (!File.Exists(boxFile))
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
            var response = client.DetectText(image);
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

            var serializer = new JsonSerializer {NullValueHandling = NullValueHandling.Ignore};

            using (StreamWriter sw = new StreamWriter(boxFile))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
               serializer.Serialize(writer, allBoxes);
               // {"ExpiryDate":new Date(1230375600000),"Price":0}
            }
            var childBoxes = allBoxes.Select(x => x.Children.Count == 0).ToArray();
            logger.Info($"end boxes {childBoxes.Length}");
         }
         boxesBySize = new SortedList<long, Box>(new DuplicateKeyComparer<long>());
         var json = File.ReadAllText(boxFile);
         var boxes = JsonConvert.DeserializeObject<List<Box>>(json);
         foreach (var b in boxes)
         {
            logger.Info(
               $"{num}. {b} cs:{b.Annotation.CalculateSize()} Mid:{b.Annotation.Mid} Parent:{b.Parent?.Index}");
            boxesBySize.Add(b.Size, b);

         }
      }
   }
}
// [END vision_quickstart]
