using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using mshtml;
using System.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WebCrawler
{
    class Program
    {
        static void Main(string[] args)
        {
            const string url = @"https://en.wikipedia.org/wiki/The_Hobbit";
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            Crawler crawler = new Crawler(1);
            crawler.Fetch(url);
            Save(crawler.documents);

            stopwatch.Stop();
            Console.WriteLine($"Found {crawler.count} documents in {stopwatch.Elapsed.Minutes}:{stopwatch.Elapsed.Seconds}");
        }

        static void Save(Dictionary<string, string> documents)
        {
            Stemmer stemmer = new Stemmer();
            List<InvertedIndexRow> list = new List<InvertedIndexRow>();
            int docid = 0;
            foreach (var document in documents.Values)
            {
                var words = document.Split(' ');

                var wordCounter = new Dictionary<string, int>();
                int position = 0;
                foreach (string word in words)
                {
                    // if stop word continue

                    string stemmedWord = stemmer.Stem(word);

                    if (!wordCounter.ContainsKey(stemmedWord))
                        wordCounter[stemmedWord] = 1;
                    else
                        wordCounter[stemmedWord] += 1;

                    var row = new InvertedIndexRow(stemmedWord, docid, 0, position);
                    list.Add(row);
                    position += 1;
                }
                foreach (var row in list)
                {
                    row.frequency = wordCounter[row.term];
                }
                docid += 1;
            }

            FileStream file = new FileStream("Inverted Index List.txt", FileMode.Create);
            StreamWriter writer = new StreamWriter(file);
            writer.WriteLine("Term\t\tDocID\t\tFreq\t\tPos\n");
            foreach (var row in list)
            {
                string s = row.term;
                for (int i = 0; i < 20 - row.term.Length; ++i)
                    s += ' ';
                s += row.docId.ToString() + "\t\t";
                s += row.frequency.ToString() + "\t\t";
                s += row.position.ToString();
                writer.WriteLine(s);
            }
            writer.Close();
            file.Close();
        }
    }

    public class InvertedIndexRow
    {
        public string term;
        public int docId;
        public int frequency;
        public int position;

        public InvertedIndexRow(string term, int docId, int frequency, int position)
        {
            this.term = term;
            this.docId = docId;
            this.frequency = frequency;
            this.position = position;
        }
    }

    public class Crawler
    {
        private int maxDocuments;
        private int maxThreads;
        private int threads;
        public int count;
        public Dictionary<string, string> documents;
        
        public Crawler(int maxDocuments = 3000, int maxThreads = -1)
        {
            this.maxDocuments = maxDocuments;
            count = 0;
            this.maxThreads = maxThreads;
            threads = 0;
            documents = new Dictionary<string, string>();
        }

        public void Fetch(object urlObj)
        {

            string url = (string)urlObj;

            WebRequest request = WebRequest.Create(url);
            WebResponse response = request.GetResponse();

            Stream stream = response.GetResponseStream();
            StreamReader reader = new StreamReader(stream);
            string responeString = reader.ReadToEnd();

            stream.Close();
            reader.Close();
            response.Close();


            IHTMLDocument2 document = new HTMLDocumentClass();
            document.write(responeString);

            string extractedParagraphs = "";
            foreach (IHTMLElement element in document.all)
            {
                if (element.tagName.Equals("P"))
                {
                    if (element.innerText != null)
                    {
                        extractedParagraphs += element.innerText + "\n";
                    }
                }           
            }
            documents.Add(url, RemoveSpecialCharacters(extractedParagraphs));
            count += 1;

            foreach (IHTMLElement element in document.links)
            {
                if (count >= maxDocuments)
                {
                    return;
                }

                string link = (string)element.getAttribute("href", 0);
                link = Fix(link);
                if (link != "" && !documents.ContainsKey(link))
                {
                    try
                    {
                        if (threads < maxThreads)
                        {
                            threads += 1;
                            Thread thread = new Thread(new ParameterizedThreadStart(Fetch));
                            thread.Start(link);
                        }
                        else
                            Fetch(link);
                    }
                    catch
                    {
                        //Console.WriteLine($"error fetching {link}");
                    }
                }
            }
        }

        private string Fix(string link)
        {
            if (link.StartsWith("about:"))
            {
                if (link.Contains(":/wiki/"))
                    return @"https://en.wikipedia.org" + link.Substring(6);
                return "";
            }

            if (link.Contains("wikipedia.org") && !link.Contains("en.wikipedia"))
            {
                return "";
            }

            return link;
        }

        public string RemoveSpecialCharacters(string input)
        {
            Regex r = new Regex("(?:[^a-z0-9 ]|(?<=['\"])s)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            return r.Replace(input, String.Empty);
        }
    }
}
