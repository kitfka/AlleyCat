﻿using System;
using System.Diagnostics;
using System.Linq;
using AlleyCat.Logging;
using EnsureThat;
using Godot;
using LanguageExt;
using static LanguageExt.Prelude;

namespace AlleyCat.Autowire
{
    internal class DependencyNode : IDependencyResolver
    {
        public Node Instance { get; }

        public HashSet<DependencyNode> Dependencies { get; private set; }

        public HashSet<Type> Requires => _resolver.Requires;

        public HashSet<Type> Provides => _resolver.Provides;

        public SortMark SortMark { get; set; } = SortMark.Unmarked;

        private readonly Action<IAutowireContext> _processor;

        private readonly IDependencyResolver _resolver;

        private readonly Option<Action<IAutowireContext>> _deferredProcessor;

        public DependencyNode(AutowireContext context)
        {
            Ensure.That(context, nameof(context)).IsNotNull();

            Instance = context.Node;
            Dependencies = HashSet<DependencyNode>();

            Debug.Assert(Instance != null, "Instance != null");

            _resolver = context;

            _processor = _ => context.Build();
        }

        public DependencyNode(Node node, ServiceDefinition definition)
        {
            Ensure.That(node, nameof(node)).IsNotNull();

            Instance = node;
            Dependencies = HashSet<DependencyNode>();

            _resolver = definition;

            _processor = context => definition.Processors
                .Where(p => p.ProcessPhase != AutowirePhase.Deferred)
                .Iter(p => ProcessNode(context, p, node));

            void ProcessDeferred(IAutowireContext context) =>
                definition.Processors
                    .Where(p => p.ProcessPhase == AutowirePhase.Deferred)
                    .Iter(p => ProcessNode(context, p, node));

            _deferredProcessor = Some((Action<IAutowireContext>) ProcessDeferred);
        }

        public void Process(IAutowireContext context) => _processor.Invoke(context);

        public void ProcessDeferred(IAutowireContext context) => _deferredProcessor.Iter(p => p.Invoke(context));

        private static void ProcessNode(IAutowireContext context, INodeProcessor processor, Node node)
        {
            try
            {
                context.LogDebug("Processing {} on '{}'.", processor, node);

                processor.Process(context, node);
            }
            catch (Exception e)
            {
                context.LogError(e, "Failed to process {} on '{}'.", processor, node);
            }
        }

        public void AddDependency(DependencyNode node)
        {
            Dependencies = Dependencies.TryAdd(node);
        }

        public void ClearDependencies()
        {
            Dependencies = HashSet<DependencyNode>();
            SortMark = SortMark.Unmarked;
        }

        public bool DependsOn(DependencyNode other) => DependsOn(other, this);

        private bool DependsOn(DependencyNode other, DependencyNode from)
        {
            if (other.Instance == from.Instance)
            {
                return false;
            }

            foreach (var node in Dependencies)
            {
                if (node.Instance == from.Instance)
                {
                    throw new CyclicDependencyException(
                        $"Found cyclic dependency on node: '{node.Instance.Name}'.");
                }

                if (node.Instance == other.Instance || node.DependsOn(other, from))
                {
                    return true;
                }
            }

            return false;
        }

        public override bool Equals(object obj) => obj is DependencyNode node && node.Instance == Instance;

        public override int GetHashCode() => Instance.GetInstanceId().ToString().GetHashCode();

        public override string ToString() => $"{nameof(DependencyNode)}[{Instance.GetPath()}]";
    }

    internal enum SortMark
    {
        Unmarked, Temporary, Permanent
    }
}
