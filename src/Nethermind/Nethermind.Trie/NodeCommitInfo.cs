using System;

namespace Nethermind.Trie
{
    public readonly struct NodeCommitInfo
    {
        public NodeCommitInfo(TrieNode node)
        {
            ChildPositionAtParent = 0;
            Node = node;
            NodeParent = null;
            FullPath = node.IsExtension || node.IsLeaf ? node.Path : Array.Empty<byte>();
        }

        public NodeCommitInfo(
            TrieNode node,
            TrieNode nodeParent,
            int childPositionAtParent)
        {
            ChildPositionAtParent = childPositionAtParent;
            Node = node;
            NodeParent = nodeParent;
            FullPath = Array.Empty<byte>();
        }

        public NodeCommitInfo(
            TrieNode node,
            TrieNode nodeParent,
            int childPositionAtParent,
            byte[] parentPath)
        {
            ChildPositionAtParent = childPositionAtParent;
            Node = node;
            NodeParent = nodeParent;
            FullPath = parentPath;

            int totalLength = parentPath.Length;
            totalLength += nodeParent.IsBranch ? 1 : 0;
            totalLength += (node.IsLeaf || node.IsExtension) ? node.Path!.Length : 0;

            if (totalLength > FullPath.Length)
            {
                FullPath = new byte[totalLength];
                Array.Copy(parentPath, FullPath, parentPath.Length);
            }

            if (nodeParent.IsBranch)
            {
                FullPath[parentPath.Length] = (byte)childPositionAtParent;
            }
            if (node.IsLeaf || node.IsExtension)
            {
                Array.Copy(node.Path, 0, FullPath, parentPath.Length + (nodeParent.IsBranch ? 1 : 0), node.Path.Length);
            }
        }

        public TrieNode? Node { get; }

        public TrieNode? NodeParent { get; }

        public int ChildPositionAtParent { get; }

        public bool IsEmptyBlockMarker => Node is null;

        public bool IsRoot => !IsEmptyBlockMarker && NodeParent is null;

        public byte[] FullPath { get; }

        public override string ToString()
        {
            return $"[{nameof(NodeCommitInfo)}|{Node}|{(NodeParent is null ? "root" : $"child {ChildPositionAtParent} of {NodeParent}")}]";
        }
    }
}
