// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Extensions;
using Nethermind.Logging;
using Nethermind.Trie.Store.Nodes;
using Nethermind.Trie.Store.Nodes.Patricia;
using Nethermind.Logging;

namespace Nethermind.Trie.Store.Tree;

public class Patricia
{
    private MerkleTreeDb _stateDb = new MerkleTreeDb();

    private readonly Stack<StackedNode> _nodeStack = new();

    private readonly ILogger _logger;

    public Patricia(ILogManager logManager)
    {
        _logger = logManager?.GetClassLogger<Patricia>() ?? throw new ArgumentNullException(nameof(logManager));
    }

    public byte[] Get(Span<byte> key)
    {
        _stateDb.GetNode(key.ToArray(), out LeafNode? node);
        return Array.Empty<byte>();

    }

    public void Set(Span<byte> key, Span<byte> value)
    {
        TraverseContext traverseContext = new TraverseContext(key, value.ToArray());

        _stateDb.GetNode(key.ToArray(), out BranchNode? node);


    }

    private byte[]? TraverseNode(IMerkleNode node, TraverseContext traverseContext)
    {
        if (_logger.IsTrace)
            _logger.Trace(
                $"Traversing {node} to {(traverseContext.IsDelete ? "DELETE" : "UPDATE")}");

        return node.NodeType switch
        {
            NodeType.Branch => TraverseBranch((BranchNode)node, traverseContext),
            NodeType.Extension => TraverseExtension((ExtensionNode)node, traverseContext),
            NodeType.Leaf => TraverseLeaf((LeafNode)node, traverseContext),
            NodeType.Unknown => throw new InvalidOperationException(
                $"Cannot traverse unresolved node {node.Keccak}"),
            _ => throw new NotSupportedException(
                $"Unknown node type {node.NodeType}")
        };
    }

    private byte[]? TraverseBranch(BranchNode node, TraverseContext traverseContext)
    {
        if (traverseContext.RemainingUpdatePathLength == 0)
        {
            /* all these cases when the path ends on the branch assume a trie with values in the branches
               which is not possible within the Ethereum protocol which has keys of the same length (64) */


            if (traverseContext.IsDelete)
            {
                if (node.Value is null)
                {
                    return null;
                }

                ConnectNodes(null);
            }
            else if (Bytes.AreEqual(traverseContext.UpdateValue, node.Value))
            {
                return traverseContext.UpdateValue;
            }
            else
            {
                node.Value = traverseContext.UpdateValue;
                ConnectNodes(node);
            }

            return traverseContext.UpdateValue;
        }

        if (!traverseContext.IsDelete)
        {
            _nodeStack.Push(new StackedNode(node, traverseContext.UpdatePath[traverseContext.CurrentIndex]));
        }


        IMerkleNode? childNode = null;
        if (_stateDb.GetNode(traverseContext.UpdatePath.Slice(0, traverseContext.CurrentIndex + 1).ToArray(), out LeafNode? _childLeafNode))
        {
            childNode = _childLeafNode;
        }
        else if (_stateDb.GetNode(traverseContext.UpdatePath.Slice(0, traverseContext.CurrentIndex + 1).ToArray(), out BranchNode? _childBranchNode))
        {
            childNode = _childBranchNode;
        }
        else if (_stateDb.GetNode(traverseContext.UpdatePath.Slice(0, traverseContext.CurrentIndex + 1).ToArray(), out ExtensionNode? _childExtensionNode))
        {
            childNode = _childExtensionNode;
        }
        else
        {
            traverseContext.CurrentIndex++;
            if (traverseContext.IsDelete)
            {
                if (traverseContext.IgnoreMissingDelete)
                {
                    return null;
                }

                throw new ArgumentException(
                    $"Could not find the leaf node to delete: {traverseContext.UpdatePath.ToHexString(false)}");
            }

            byte[] leafPath = traverseContext.UpdatePath.Slice(
                traverseContext.CurrentIndex,
                traverseContext.UpdatePath.Length - traverseContext.CurrentIndex).ToArray();
            LeafNode leaf = new LeafNode(leafPath)
            {
                Value = traverseContext.UpdateValue, Path = traverseContext.GetCurrentPath().ToArray()

            };
            ConnectNodes(leaf);

            return traverseContext.UpdateValue;
        }
        traverseContext.CurrentIndex++;
        return TraverseNode(childNode, traverseContext);
    }

    private byte[]? TraverseLeaf(LeafNode node, TraverseContext traverseContext)
    {
        if (node.Path is null)
        {
            throw new InvalidDataException("An attempt to visit a node without a prefix path.");
        }

        Span<byte> remaining = traverseContext.GetRemainingUpdatePath();
        Span<byte> shorterPath;
        Span<byte> longerPath;
        if (traverseContext.RemainingUpdatePathLength - node.Path.Length < 0)
        {
            shorterPath = remaining;
            longerPath = node.Path;
        }
        else
        {
            shorterPath = node.Path;
            longerPath = remaining;
        }

        byte[] shorterPathValue;
        byte[] longerPathValue;

        if (Bytes.AreEqual(shorterPath, node.Path))
        {
            shorterPathValue = node.Value;
            longerPathValue = traverseContext.UpdateValue;
        }
        else
        {
            shorterPathValue = traverseContext.UpdateValue;
            longerPathValue = node.Value;
        }

        int extensionLength = FindCommonPrefixLength(shorterPath, longerPath);
        if (extensionLength == shorterPath.Length && extensionLength == longerPath.Length)
        {

            if (traverseContext.IsDelete)
            {
                ConnectNodes(null);
                return traverseContext.UpdateValue;
            }

            if (!Bytes.AreEqual(node.Value, traverseContext.UpdateValue))
            {
                node.Value = traverseContext.UpdateValue;
                ConnectNodes(node);
                return traverseContext.UpdateValue;
            }

            return traverseContext.UpdateValue;
        }


        if (traverseContext.IsDelete)
        {
            if (traverseContext.IgnoreMissingDelete)
            {
                return null;
            }

            throw new ArgumentException(
                $"Could not find the leaf node to delete: {traverseContext.UpdatePath.ToHexString(false)}");
        }

        if (extensionLength != 0)
        {
            Span<byte> extensionPath = longerPath.Slice(0, extensionLength);
            ExtensionNode extension = new ExtensionNode()
            {
                Key = extensionPath.ToArray(), Path = traverseContext.GetCurrentPath().ToArray()
            };
            _nodeStack.Push(new StackedNode(extension, 0));
        }

        BranchNode branch =
        TrieNode branch = TrieNodeFactory.CreateBranch(traverseContext.UpdatePath.Slice(0, traverseContext.CurrentIndex + extensionLength).ToArray());
        if (extensionLength == shorterPath.Length)
        {
            branch.Value = shorterPathValue;
        }
        else
        {
            Span<byte> shortLeafPath = shorterPath.Slice(extensionLength + 1, shorterPath.Length - extensionLength - 1);
            TrieNode shortLeaf;
            if (shorterPath.Length == 64)
            {
                Span<byte> pathToShortLeaf = shorterPath.Slice(0, extensionLength + 1);
                shortLeaf = TrieNodeFactory.CreateLeaf(HexPrefix.Leaf(shortLeafPath.ToArray()), shorterPathValue, pathToShortLeaf);
            }
            else
            {
                Span<byte> pathToShortLeaf = stackalloc byte[branch.PathToNode.Length + 1];
                branch.PathToNode.CopyTo(pathToShortLeaf);
                pathToShortLeaf[branch.PathToNode.Length] = shorterPath[extensionLength];
                shortLeaf = TrieNodeFactory.CreateLeaf(HexPrefix.Leaf(shortLeafPath.ToArray()), shorterPathValue, pathToShortLeaf);
            }
            branch.SetChild(shorterPath[extensionLength], shortLeaf);
        }

        Span<byte> leafPath = longerPath.Slice(extensionLength + 1, longerPath.Length - extensionLength - 1);
        Span<byte> pathToLeaf = stackalloc byte[branch.PathToNode.Length + 1];
        branch.PathToNode.CopyTo(pathToLeaf);
        pathToLeaf[branch.PathToNode.Length] = longerPath[extensionLength];
        TrieNode withUpdatedKeyAndValue = node.CloneWithChangedKeyAndValue(
            HexPrefix.Leaf(leafPath.ToArray()), longerPathValue, pathToLeaf.ToArray());

        _nodeStack.Push(new StackedNode(branch, longerPath[extensionLength]));
        ConnectNodes(withUpdatedKeyAndValue);

        return traverseContext.UpdateValue;
    }

    private byte[]? TraverseExtension(ExtensionNode node, TraverseContext traverseContext)
    {
        if (node.Path is null)
        {
            throw new InvalidDataException("An attempt to visit a node without a prefix path.");
        }

        TrieNode originalNode = node;
        Span<byte> remaining = traverseContext.GetRemainingUpdatePath();

        int extensionLength = FindCommonPrefixLength(remaining, node.Path);
        if (extensionLength == node.Path.Length)
        {
            traverseContext.CurrentIndex += extensionLength;
            if (traverseContext.IsUpdate)
            {
                _nodeStack.Push(new StackedNode(node, 0));
            }

            TrieNode next = node.GetChild(TrieStore, traverseContext.GetCurrentPath(), 0);
            if (next is null)
            {
                throw new ArgumentException(
                    $"Found an {nameof(NodeType.Extension)} {node.Keccak} that is missing a child.");
            }

            //next.ResolveNode(TrieStore, traverseContext.GetCurrentPath());
            next.ResolveNode(TrieStore);
            return TraverseNode(next, traverseContext);
        }

        if (traverseContext.IsRead)
        {
            return null;
        }

        if (traverseContext.IsDelete)
        {
            if (traverseContext.IgnoreMissingDelete)
            {
                return null;
            }

            throw new ArgumentException(
                $"Could find the leaf node to delete: {traverseContext.UpdatePath.ToHexString()}");
        }

        byte[] pathBeforeUpdate = node.Path;
        if (extensionLength != 0)
        {
            byte[] extensionPath = node.Path.Slice(0, extensionLength);
            node = node.CloneWithChangedKey(HexPrefix.Extension(extensionPath));
            _nodeStack.Push(new StackedNode(node, 0));
        }

        TrieNode branch = TrieNodeFactory.CreateBranch(traverseContext.UpdatePath.Slice(0, traverseContext.CurrentIndex + extensionLength));
        if (extensionLength == remaining.Length)
        {
            branch.Value = traverseContext.UpdateValue;
        }
        else
        {
            byte[] path = remaining.Slice(extensionLength + 1, remaining.Length - extensionLength - 1).ToArray();
            TrieNode shortLeaf = TrieNodeFactory.CreateLeaf(HexPrefix.Leaf(path), traverseContext.UpdateValue, traverseContext.UpdatePath.Slice(0, traverseContext.CurrentIndex + extensionLength + 1));
            branch.SetChild(remaining[extensionLength], shortLeaf);
        }

        TrieNode originalNodeChild = originalNode.GetChild(TrieStore, 0);
        if (originalNodeChild is null)
        {
            throw new InvalidDataException(
                $"Extension {originalNode.Keccak} has no child.");
        }

        if (pathBeforeUpdate.Length - extensionLength > 1)
        {
            byte[] extensionPath = pathBeforeUpdate.Slice(extensionLength + 1, pathBeforeUpdate.Length - extensionLength - 1);
            Span<byte> fullPath = traverseContext.UpdatePath.Slice(0, traverseContext.CurrentIndex + extensionLength + 1);
            fullPath[traverseContext.CurrentIndex + extensionLength] = pathBeforeUpdate[extensionLength];
            TrieNode secondExtension
                = TrieNodeFactory.CreateExtension(HexPrefix.Extension(extensionPath), originalNodeChild, fullPath);
            branch.SetChild(pathBeforeUpdate[extensionLength], secondExtension);
        }
        else
        {
            TrieNode childNode = originalNodeChild;
            branch.SetChild(pathBeforeUpdate[extensionLength], childNode);
        }

        ConnectNodes(branch);
        return traverseContext.UpdateValue;
    }

    private void ConnectNodes(IMerkleNode? node)
    {
        bool isRoot = _nodeStack.Count == 0;
        TrieNode nextNode = node;

        while (!isRoot)
        {
            StackedNode parentOnStack = _nodeStack.Pop();
            node = parentOnStack.Node;

            isRoot = _nodeStack.Count == 0;

            if (node.IsLeaf)
            {
                throw new ArgumentException(
                    $"{nameof(NodeType.Leaf)} {node} cannot be a parent of {nextNode}");
            }

            if (node.IsBranch)
            {
                if (!(nextNode is null && !node.IsValidWithOneNodeLess))
                {
                    if (node.IsSealed)
                    {
                        node = node.Clone();
                    }

                    node.SetChild(parentOnStack.PathIndex, nextNode);
                    nextNode = node;
                }
                else
                {
                    if (node.Value!.Length != 0)
                    {
                        // this only happens when we have branches with values
                        // which is not possible in the Ethereum protocol where keys are of equal lengths
                        // (it is possible in the more general trie definition)
                        TrieNode leafFromBranch = TrieNodeFactory.CreateLeaf(HexPrefix.Leaf(), node.Value);
                        if (_logger.IsTrace) _logger.Trace($"Converting {node} into {leafFromBranch}");
                        nextNode = leafFromBranch;
                    }
                    else
                    {
                        /* all the cases below are when we have a branch that becomes something else
                           as a result of deleting one of the last two children */
                        /* case 1) - extension from branch
                           this is particularly interesting - we create an extension from
                           the implicit path in the branch children positions (marked as P)
                           P B B B B B B B B B B B B B B B
                           B X - - - - - - - - - - - - - -
                           case 2) - extended extension
                           B B B B B B B B B B B B B B B B
                           E X - - - - - - - - - - - - - -
                           case 3) - extended leaf
                           B B B B B B B B B B B B B B B B
                           L X - - - - - - - - - - - - - - */

                        int childNodeIndex = 0;
                        for (int i = 0; i < 16; i++)
                        {
                            if (i != parentOnStack.PathIndex && !node.IsChildNull(i))
                            {
                                childNodeIndex = i;
                                break;
                            }
                        }

                        TrieNode childNode = node.GetChild(TrieStore, childNodeIndex);
                        if (childNode is null)
                        {
                            /* potential corrupted trie data state when we find a branch that has only one child */
                            throw new ArgumentException(
                                "Before updating branch should have had at least two non-empty children");
                        }

                        childNode.ResolveNode(TrieStore);
                        if (childNode.IsBranch)
                        {
                            TrieNode extensionFromBranch =
                                TrieNodeFactory.CreateExtension(
                                    HexPrefix.Extension((byte)childNodeIndex), childNode);
                            if (_logger.IsTrace)
                                _logger.Trace(
                                    $"Extending child {childNodeIndex} {childNode} of {node} into {extensionFromBranch}");

                            nextNode = extensionFromBranch;
                        }
                        else if (childNode.IsExtension)
                        {
                            /* to test this case we need something like this initially */
                            /* R
                               B B B B B B B B B B B B B B B B
                               E L - - - - - - - - - - - - - -
                               E - - - - - - - - - - - - - - -
                               B B B B B B B B B B B B B B B B
                               L L - - - - - - - - - - - - - - */

                            /* then we delete the leaf (marked as X) */
                            /* R
                               B B B B B B B B B B B B B B B B
                               E X - - - - - - - - - - - - - -
                               E - - - - - - - - - - - - - - -
                               B B B B B B B B B B B B B B B B
                               L L - - - - - - - - - - - - - - */

                            /* and we end up with an extended extension (marked with +)
                               replacing what was previously a top-level branch */
                            /* R
                               +
                               +
                               + - - - - - - - - - - - - - - -
                               B B B B B B B B B B B B B B B B
                               L L - - - - - - - - - - - - - - */

                            HexPrefix newKey
                                = HexPrefix.Extension(Bytes.Concat((byte)childNodeIndex, childNode.Path));
                            TrieNode extendedExtension = childNode.CloneWithChangedKey(newKey);
                            if (_logger.IsTrace)
                                _logger.Trace(
                                    $"Extending child {childNodeIndex} {childNode} of {node} into {extendedExtension}");
                            nextNode = extendedExtension;
                        }
                        else if (childNode.IsLeaf)
                        {
                            HexPrefix newKey = HexPrefix.Leaf(Bytes.Concat((byte)childNodeIndex, childNode.Path));
                            TrieNode extendedLeaf = childNode.CloneWithChangedKey(newKey);
                            if (_logger.IsTrace)
                                _logger.Trace(
                                    $"Extending branch child {childNodeIndex} {childNode} into {extendedLeaf}");

                            if (_logger.IsTrace) _logger.Trace($"Decrementing ref on a leaf extended up to eat a branch {childNode}");
                            if (node.IsSealed)
                            {
                                if (_logger.IsTrace) _logger.Trace($"Decrementing ref on a branch replaced by a leaf {node}");
                            }

                            nextNode = extendedLeaf;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unknown node type {childNode.NodeType}");
                        }
                    }
                }
            }
            else if (node.IsExtension)
            {
                if (nextNode is null)
                {
                    throw new InvalidOperationException(
                        $"An attempt to set a null node as a child of the {node}");
                }

                if (nextNode.IsLeaf)
                {
                    HexPrefix newKey = HexPrefix.Leaf(Bytes.Concat(node.Path, nextNode.Path));
                    TrieNode extendedLeaf = nextNode.CloneWithChangedKey(newKey);
                    if (_logger.IsTrace)
                        _logger.Trace($"Combining {node} and {nextNode} into {extendedLeaf}");

                    nextNode = extendedLeaf;
                }
                else if (nextNode.IsExtension)
                {
                    /* to test this case we need something like this initially */
                    /* R
                       E - - - - - - - - - - - - - - -
                       B B B B B B B B B B B B B B B B
                       E L - - - - - - - - - - - - - -
                       E - - - - - - - - - - - - - - -
                       B B B B B B B B B B B B B B B B
                       L L - - - - - - - - - - - - - - */

                    /* then we delete the leaf (marked as X) */
                    /* R
                       B B B B B B B B B B B B B B B B
                       E X - - - - - - - - - - - - - -
                       E - - - - - - - - - - - - - - -
                       B B B B B B B B B B B B B B B B
                       L L - - - - - - - - - - - - - - */

                    /* and we end up with an extended extension replacing what was previously a top-level branch*/
                    /* R
                       E
                       E
                       E - - - - - - - - - - - - - - -
                       B B B B B B B B B B B B B B B B
                       L L - - - - - - - - - - - - - - */

                    HexPrefix newKey
                        = HexPrefix.Extension(Bytes.Concat(node.Path, nextNode.Path));
                    TrieNode extendedExtension = nextNode.CloneWithChangedKey(newKey);
                    if (_logger.IsTrace)
                        _logger.Trace($"Combining {node} and {nextNode} into {extendedExtension}");

                    nextNode = extendedExtension;
                }
                else if (nextNode.IsBranch)
                {
                    if (node.IsSealed)
                    {
                        node = node.Clone();
                    }

                    if (_logger.IsTrace) _logger.Trace($"Connecting {node} with {nextNode}");
                    node.SetChild(0, nextNode);
                    nextNode = node;
                }
                else
                {
                    throw new InvalidOperationException($"Unknown node type {nextNode.NodeType}");
                }
            }
            else
            {
                throw new InvalidOperationException($"Unknown node type {node.GetType().Name}");
            }
        }

        RootRef = nextNode;
    }

    private static int FindCommonPrefixLength(Span<byte> shorterPath, Span<byte> longerPath)
    {
        int commonPrefixLength = 0;
        int maxLength = Math.Min(shorterPath.Length, longerPath.Length);
        for (int i = 0; i < maxLength && shorterPath[i] == longerPath[i]; i++, commonPrefixLength++)
        {
            // just finding the common part of the path
        }

        return commonPrefixLength;
    }

    private ref struct TraverseContext
    {
        public Span<byte> UpdatePath { get; }
        public byte[]? UpdateValue { get; }
        public bool IsDelete => UpdateValue is null;
        public bool IgnoreMissingDelete { get; }
        public int CurrentIndex { get; set; }
        public int RemainingUpdatePathLength => UpdatePath.Length - CurrentIndex;

        public Span<byte> GetRemainingUpdatePath()
        {
            return UpdatePath.Slice(CurrentIndex, RemainingUpdatePathLength);
        }

        public Span<byte> GetCurrentPath()
        {
            return UpdatePath.Slice(0, CurrentIndex);
        }

        public TraverseContext(
            Span<byte> updatePath,
            byte[]? updateValue,
            bool ignoreMissingDelete = true)
        {
            UpdatePath = updatePath;
            if (updateValue is not null && updateValue.Length == 0)
            {
                updateValue = null;
            }

            UpdateValue = updateValue;
            IgnoreMissingDelete = ignoreMissingDelete;
            CurrentIndex = 0;
        }

        public override string ToString() => $"{(IsDelete ? "DELETE" : "UPDATE")} {UpdatePath.ToHexString()}{($" -> {UpdateValue}")}";

    }

    private readonly struct StackedNode
    {
        public StackedNode(IMerkleNode node, int pathIndex)
        {
            Node = node;
            PathIndex = pathIndex;
        }

        public IMerkleNode Node { get; }
        public int PathIndex { get; }

        public override string ToString()
        {
            return $"{PathIndex} {Node}";
        }
    }
}
