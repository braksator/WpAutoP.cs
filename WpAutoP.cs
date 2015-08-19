using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WpAutoP
{
    public class WpAutoP
    {

        /**
         * A C# implementation of wpautop() from wordpress.
         */
        public static string WpAutoP(string pee, bool br = true)
        {
            var pre_tags = new Dictionary<string, string>();

            if (pee.Trim() == "")
            {
                return "";
            }

            // Just to make things a little easier, pad the end.
            pee = pee + "\n";

            /*
             * Pre tags shouldn't be touched by autop.
             * Replace pre tags with placeholders and bring them back after autop.
             */
            if (pee.Contains("<pre"))
            {
                Stack<string> pee_parts = new Stack<string>(pee.Split(new string[] { "</pre>" }, StringSplitOptions.None));
                string last_pee = pee_parts.Pop();
                pee = "";
                int i = 0;

                foreach (var pee_part in pee_parts)
                {
                    int start = pee_part.IndexOf("<pre");

                    // Malformed html?
                    if (start == -1)
                    {
                        pee += pee_part;
                        continue;
                    }

                    string name = "<pre wp-pre-tag-" + i + "></pre>";
                    pre_tags[name] = pee_part.Substring(start) + "</pre>";

                    pee += pee_part.Substring(0, start) + name;
                    i++;
                }

                pee += last_pee;
            }
            // Change multiple <br>s into two line breaks, which will turn into paragraphs.
            string text = Regex.Replace(pee, @"<br />\s*<br />", "\n\n");

            string allblocks = "(?:table|thead|tfoot|caption|col|colgroup|tbody|tr|td|th|div|dl|dd|dt|ul|ol|li|pre|form|map|area|blockquote|address|math|style|p|h[1-6]|hr|fieldset|legend|section|article|aside|hgroup|header|footer|nav|figure|figcaption|details|menu|summary)";

            // Add a single line break above block-level opening tags.
            pee = Regex.Replace(pee, "(<" + allblocks + "[^>]*>)", "\n$1");

            // Add a double line break below block-level closing tags.
            pee = Regex.Replace(pee, "(</" + allblocks + ">)", "$1\n\n");

            // Standardize newline characters to "\n".
            pee = pee.Replace("\r\n", "\n");
            pee = pee.Replace("\r", "\n");

            // Collapse line breaks before and after <option> elements so they don't get autop'd.
            if (pee.IndexOf("<option") != -1)
            {
                pee = Regex.Replace(pee, @"\s*<option", "<option");
                pee = Regex.Replace(pee, @"</option>\s*", "</option>");
            }

            /*
             * Collapse line breaks inside <object> elements, before <param> and <embed> elements
             * so they don't get autop'd.
             */
            if (pee.IndexOf("</object>") != -1)
            {
                pee = Regex.Replace(pee, @"(<object[^>]*>)\s*", "$1");
                pee = Regex.Replace(pee, @"\s*</object>", "</object>");
                pee = Regex.Replace(pee, @"%\s*(</?(?:param|embed)[^>]*>)\s*%", "$1");
            }

            /*
             * Collapse line breaks inside <audio> and <video> elements,
             * before and after <source> and <track> elements.
             */
            if (pee.IndexOf("<source") != -1 || pee.IndexOf("<track") != -1)
            {
                pee = Regex.Replace(pee, @"([<\[](?:audio|video)[^>\]]*[>\]])\s*", "$1");
                pee = Regex.Replace(pee, @"\s*([<\[]/(?:audio|video)[>\]])", "$1");
                pee = Regex.Replace(pee, @"\s*(<(?:source|track)[^>]*>)\s*", "$1");
            }

            // Remove more than two contiguous line breaks.
            pee = Regex.Replace(pee, "/\n\n+/", "\n\n");

            // Split up the contents into an array of strings, separated by double line breaks.
            string[] pees = Regex.Split(pee, @"\n\s*\n");
            pees = pees.Where(x => !string.IsNullOrEmpty(x)).ToArray();

            // Reset pee prior to rebuilding.
            pee = "";

            // Rebuild the content as a string, wrapping every bit with a <p>.
            foreach (string tinkle in pees)
            {
                pee += "<p>" + tinkle.Trim('\n') + "</p>\n";
            }

            // Under certain strange conditions it could create a P of entirely whitespace.
            pee = Regex.Replace(pee, @"<p>\s*</p>", "");

            // Add a closing <p> inside <div>, <address>, or <form> tag if missing.
            pee = Regex.Replace(pee, "<p>([^<]+)</(div|address|form)>", "<p>$1</p></$2>");

            // If an opening or closing block element tag is wrapped in a <p>, unwrap it.
            pee = Regex.Replace(pee, @"<p>\s*(</?" + allblocks + @"[^>]*>)\s*</p>", "$1");

            // In some cases <li> may get wrapped in <p>, fix them.
            pee = Regex.Replace(pee, "<p>(<li.+?)</p>", "$1");

            // If a <blockquote> is wrapped with a <p>, move it inside the <blockquote>.
            pee = Regex.Replace(pee, "(?i)<p><blockquote([^>]*)>", "<blockquote$1><p>");
            pee = pee.Replace("</blockquote></p>", "</p></blockquote>");

            // If an opening or closing block element tag is preceded by an opening <p> tag, remove it.
            pee = Regex.Replace(pee, @"<p>\s*(</?" + allblocks + "[^>]*>)", "$1");

            // If an opening or closing block element tag is followed by a closing <p> tag, remove it.
            pee = Regex.Replace(pee, "(</?" + allblocks + @"[^>]*>)\s*</p>", "$1");

            // Optionally insert line breaks.
            if (br)
            {
                // Replace newlines that shouldn't be touched with a placeholder.
                Regex re = new Regex(@"(?m)<(script|style).*?<\/\\1>");
                pee = re.Replace(pee, new MatchEvaluator(AutopNewlinePreservationHelper));

                // Replace any new line characters that aren't preceded by a <br /> with a <br />.
                pee = Regex.Replace(pee, @"(?<!<br />)\s*\n", "<br />\n");

                // Replace newline placeholders with newlines.
                pee = pee.Replace("<WPPreserveNewline />", "\n");
            }

            // If a <br /> tag is after an opening or closing block tag, remove it.
            pee = Regex.Replace(pee, "(</?" + allblocks + @"[^>]*>)\s*<br />", "$1");

            // If a <br /> tag is before a subset of opening or closing block tags, remove it.
            pee = Regex.Replace(pee, @"<br />(\s*</?(?:p|li|div|dl|dd|dt|th|pre|td|ul|ol)[^>]*>)", "$1");
            pee = Regex.Replace(pee, "\n</p>$", "</p>");

            // Replace placeholder <pre> tags with their original content.
            if (pre_tags.Count > 0)
            {
                foreach (var item in pre_tags)
                {
                    pee = pee.Replace(item.Key, item.Value);
                }
            }

            return pee;
        }

        static string AutopNewlinePreservationHelper(Match match)
        {
            StringBuilder sb = new StringBuilder(match.Value);
            return sb[0].ToString().Replace("\n", "<WPPreserveNewline />");
        }

    }
}
