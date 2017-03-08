using System;
using System.Collections.Generic;

namespace WebRole1
{
    // c -> ca, ca -> cat or ca -> cab
    // Each node stores 1 character and its children
    public class Node
    {
        // Fields
        private char character;         // a-z or _ or $ (not case sensitive, everything is lowercase)
        private List<Node> children;    // Downwards (0-N nodes, should be <= 28)
        private int depth;              // # Position in the word the node is (used for the misspelled words extra credit)

        //private List<string> hybridTrieChildren; // If the next 10 children

        // Constructor
        public Node(char character, int depth)
        {
            this.character = char.ToLower(character);
            this.depth = depth;
            children = new List<Node>();
        }

        // Access node's character
        public char GetCharacter()
        {
            return character;
        }

        // Access node's children
        public List<Node> GetChildren()
        {
            return children;
        }

        // Access depth
        public int GetDepth()
        {
            return depth;
        }

        // Access node's specific child with specific char (null if it doesn't exist)
        public Node GetChild(char c)
        {
            foreach (Node child in children)
            {
                if (child.GetCharacter() == Char.ToLower(c))
                {
                    return child;
                }
            }
            return null;
        }

        // Add a child to the node's children
        public void AddChild(Node newChild)
        {
            children.Add(newChild);
        }

        // Recursively traces 10 finished words from the current node
        public List<string> TraceWords(string prefix)
        {
            List<string> endpointWords = new List<string>(); // The finished word originating from the prefix

            // Loop through the children and search for endpoints
            foreach (Node child in children)
            {
                char c = child.GetCharacter();
                if (c == '$')
                {
                    prefix = prefix.Replace("_", " "); // Replace underscores with spaces
                    endpointWords.Add(prefix);
                    //System.Diagnostics.Debug.WriteLine("New endpoint: " + prefix);
                }
                else
                {
                    List<string> thisNodeEndpointWords = child.TraceWords(prefix + c);
                    foreach (string word in thisNodeEndpointWords)
                    {
                        endpointWords.Add(word);

                        // Once there are 10 endpoint words, return them without going through the other children
                        if (endpointWords.Count >= 10)
                        {
                            return endpointWords;
                        }
                    }
                }
            }
            return endpointWords;
        }
    }
}