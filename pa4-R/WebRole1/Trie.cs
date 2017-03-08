using System;
using System.Collections.Generic;

namespace WebRole1
{
    public class Trie
    {
        // Fields
        private Node originNode;

        // Constructor
        public Trie()
        {
            originNode = new Node('^', 0); // The original node from which all others are branched
        }

        // Creates path of nodes towards a title
        public void AddTitle(string title)
        {
            Node previous = originNode;
            Node current = null;

            title += "$"; // Add end of line character to the end of the title

            //System.Diagnostics.Debug.WriteLine("=====" + title + "=====");

            // For each character in the title string:
            for (int i = 0; i < title.Length; i++) //each (char c in title)
            {
                char c = title[i];

                Node existingChild = previous.GetChild(c);
                // Create the next node for c if it does not exist, or get the currently existing node for that char path
                if (Object.ReferenceEquals(null, existingChild))
                {
                    current = new Node(c, i);
                    previous.AddChild(current);
                    //System.Diagnostics.Debug.WriteLine("New node " + c + " at " + i);
                }
                else
                {
                    current = existingChild;
                    //System.Diagnostics.Debug.WriteLine("(Node " + c + " already exists at " + i + ")");
                }
                previous = current;
            }
        }

        // Path towards a node then recursively find the endpoints of the node's children's children's children's etc.
        public List<string> SearchForPrefix(string query)
        {
            var suggestions = new List<string>();

            Node current = originNode;

            for (var i = 0; i < query.Length; i++)
            {
                Node nextNode = current.GetChild(query[i]);

                if (Object.ReferenceEquals(null, nextNode))
                {
                    //System.Diagnostics.Debug.WriteLine("No results found for " + query);
                    return suggestions;
                }
                else
                {
                    current = nextNode;
                }
            }
            if (query.Length >= 1)
            {
                suggestions = current.TraceWords(query);
            }

            return suggestions;
        }
    }
}