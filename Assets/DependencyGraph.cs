// Copyright (c) 2013 Zachary Latta
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions: The above copyright
// notice and this permission notice shall be included in all copies or
// substantial portions of the Software.  THE SOFTWARE IS PROVIDED "AS IS",
// WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
// TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
// CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;

/// <summary>
/// A dependency graph is a graph-like data structure where nodes depend on
/// other nodes and can merge back together.
///
/// The dependency graph consists of a series of nodes that contain a list of
/// nodes before it, data, and a list of nodes after it. The following is a
/// graphical representation of a node.
///
/// <code>
/// +------------------------------------------+
/// |                                          |
/// |                     C                    |
/// |                                          |
/// +------------------------------------------+
/// |            |               |             |
/// |  Parents:  |  Data:        |  Children:  |
/// |  * A       |  * Fields     |  * D        |
/// |  * B       |  * Functions  |  * E        |
/// |            |               |  * F        |
/// |            |               |             |
/// +------------------------------------------+
/// </code>
///
/// The above appears in the following graphical depiction of a dependency
/// graph.
///
/// <code>
///    S
///   / \
///  A   B
///   \ /
///    C
///   /|\
///  / | \
/// D  E  F
///  \ | /
///   \|/
///    G
/// </code>
///
/// The following are graphical depictions of valid dependency graphs.
///
/// <code>
///      S             S
///     / \           / \
///    A   B         A   \
///   / \   \        |\  |\
///  C   D   E       | \ / \
///   \ /   /        |  B   D
///    F   /         | /   /
///     \ /          |/   /
///      G           C   /
///                   \ /
///                    E
/// </code>
/// </summary>

public delegate void DependencyGraphVisitor<T>(T nodeData);

class DependencyGraph<T>
{
  public T data { get; set; }
  List<DependencyGraph<T>> parents { get; set; }
  List<DependencyGraph<T>> children { get; set; }

  public DependencyGraph(T data)
  {
    this.data = data;
    parents = new List<DependencyGraph<T>>();
    children = new List<DependencyGraph<T>>();
  }

  public void addParent(T data)
  {
    DependencyGraph<T> node = new DependencyGraph<T>(data);
    insertParent(node);
  }

  public void insertParent(DependencyGraph<T> node)
  {
    node.children.Add(this);
    parents.Add(node);
  }

  public DependencyGraph<T> getParent(int i)
  {
    return parents[i];
  }

  public void addChild(T data)
  {
    DependencyGraph<T> node = new DependencyGraph<T>(data);
    insertChild(node);
  }

  public void insertChild(DependencyGraph<T> node)
  {
    node.parents.Add(this);
    children.Add(node);
  }

  public DependencyGraph<T> getChild(int i)
  {
    return children[i];
  }

  public void traverseParents(DependencyGraph<T> node,
      DependencyGraphVisitor<T> visitor)
  {
    visitor(node.data);
    foreach (DependencyGraph<T> n in node.parents)
    {
      traverseParents(n, visitor);
    }
  }

  public void traverseChildren(DependencyGraph<T> node,
      DependencyGraphVisitor<T> visitor)
  {
    visitor(node.data);
    foreach (DependencyGraph<T> n in node.children)
    {
      traverseChildren(n, visitor);
    }
  }
}
