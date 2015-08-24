// This file is never used, but I'm leaving it here because it brings back good memories.
// Thanks Zach!

// Written in 2013 by Zachary Latta
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

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
