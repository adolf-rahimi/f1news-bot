using System;
using System.Net.Http;
//using System.Threading.Tasks;
using HtmlAgilityPack;
using Telegram.Bot;
using Telegram.Bot.Types.InputFiles;
using System.Timers;
using Google.Cloud.Translation.V2;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

class Program
{
    private static readonly string TelegramBotToken = "7927339412:AAHnB87IL8oPVThoDNJiPxlqpYRUywzVFYg";
    private static readonly string TelegramChannelId = "https://t.me/Persian_Formula1";
    private static readonly HttpClient client = new HttpClient();
    private static readonly TelegramBotClient bot = new TelegramBotClient(TelegramBotToken);
    private static readonly TranslationClient translator = TranslationClient.Create();

    static async Task Main()
    {
        System.Timers.Timer timer = new System.Timers.Timer(1800000); // اجرا هر 30 دقیقه
        timer.Elapsed += async (sender, e) => await FetchAndSendNews();
        timer.Start();

        Console.WriteLine("News bot started...");
        await FetchAndSendNews(); // اجرای اولیه
        Console.ReadLine();
    }

    static async Task FetchAndSendNews()
    {
        List<string> sources = new List<string>
        {
            "https://www.motorsport.com/f1/news/",
            "https://www.autosport.com/f1/news/",
            "https://www.racingnews365.com/f1/news",
            "https://www.skysports.com/f1/news",
            "https://www.formula1.com/en/latest/all.html"
        };

        foreach (var source in sources)
        {
            var newsList = await GetNewsFromSource(source);
            foreach (var (message, imageUrl) in newsList)
            {
                await SendToTelegram(message, imageUrl);
            }
        }
    }

    static async Task<List<(string, string)>> GetNewsFromSource(string url)
    {
        var response = await client.GetStringAsync(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(response);

        var articles = doc.DocumentNode.SelectNodes("//article");
        var newsList = new List<(string, string)>();
        int count = 0;

        if (articles != null)
        {
            foreach (var article in articles)
            {
                if (count >= 3) break;

                var titleNode = article.SelectSingleNode(".//h3") ?? article.SelectSingleNode(".//h2");
                var linkNode = article.SelectSingleNode(".//a");
                var imgNode = article.SelectSingleNode(".//img");
                if (titleNode != null && linkNode != null)
                {
                    string title = titleNode.InnerText.Trim();
                    string link = linkNode.GetAttributeValue("href", "");
                    if (!link.StartsWith("http")) link = url.TrimEnd('/') + link;
                    string content = await GetNewsContent(link);
                    string translatedTitle = translator.TranslateText(title, "fa").TranslatedText;
                    string translatedContent = translator.TranslateText(content, "fa").TranslatedText;
                    string imageUrl = imgNode?.GetAttributeValue("src", "");
                    if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("http"))
                        imageUrl = url.TrimEnd('/') + imageUrl;

                    newsList.Add(($"🏁 {translatedTitle}\n\n{translatedContent}\n\n🔗 لینک خبر: {link}", imageUrl));
                    count++;
                }
            }
        }
        return newsList;
    }

    static async Task<string> GetNewsContent(string url)
    {
        var response = await client.GetStringAsync(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(response);
        var paragraphs = doc.DocumentNode.SelectNodes("//p");
        if (paragraphs == null) return "";
        string content = string.Join(" ", paragraphs.Select(p => p.InnerText).Take(3));
        return content;
    }

    static async Task SendToTelegram(string message, string imageUrl)
    {
        if (!string.IsNullOrEmpty(imageUrl))
        {
            using (var stream = await client.GetStreamAsync(imageUrl))
            {
                var inputFile = new InputOnlineFile(stream, "news.jpg");
                await bot.SendPhotoAsync(TelegramChannelId, inputFile, caption: message);
            }
        }
        else
        {
            await bot.SendTextMessageAsync(TelegramChannelId, message);
        }
    }
}
