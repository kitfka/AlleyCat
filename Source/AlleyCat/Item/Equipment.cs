using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using AlleyCat.Common;
using AlleyCat.Game;
using AlleyCat.Logging;
using AlleyCat.Morph;
using EnsureThat;
using Godot;
using LanguageExt;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;
using Object = Godot.Object;

namespace AlleyCat.Item
{
    public class Equipment : DelegateNode<RigidBody>, IEquipment
    {
        public string Key { get; }

        public virtual string DisplayName { get; }

        public virtual Option<string> Description { get; }

        public EquipmentType EquipmentType { get; }

        public string Slot => Configuration.Slot;

        public Set<string> AdditionalSlots => Configuration.AdditionalSlots;

        public EquipmentConfiguration Configuration => ActiveConfiguration.IfNone(_configurations.Head);

        public Option<EquipmentConfiguration> ActiveConfiguration => _configurations.Find(c => c.Active);

        public Map<string, EquipmentConfiguration> Configurations { get; }

        public IMorphSet Morphs { get; }

        public override bool Valid => base.Valid && Object.IsInstanceValid(Node);

        public bool Visible
        {
            get => Node.Visible;
            set => Node.Visible = value;
        }

        public IObservable<bool> OnVisibilityChange => Node.OnVisibilityChange();

        public bool Equipped => ActiveConfiguration.IsSome && Node.FindClosestAncestor<IEquipmentHolder>().IsSome;

        public Spatial Spatial => Node;

        public Godot.Mesh ItemMesh { get; }

        public MeshInstance Mesh { get; }

        public IEnumerable<CollisionShape> Colliders { get; }

        public IEnumerable<MeshInstance> Meshes => Seq1(Mesh).Filter(m => m.Visible);

        public AABB Bounds => Meshes.Select(m => m.GetAabb()).Aggregate((b1, b2) => b1.Merge(b2));

        public Map<string, Marker> Markers { get; }

        public Vector3 LabelPosition => _labelMarker.Map(m => m.GlobalTransform.origin).IfNone(this.Center);

        Node ISlotItem.Node => Node;

        private readonly IEnumerable<EquipmentConfiguration> _configurations;

        private readonly Option<Marker> _labelMarker;

        public Equipment(
            string key,
            string displayName,
            Option<string> description,
            EquipmentType equipmentType,
            IEnumerable<EquipmentConfiguration> configurations,
            IEnumerable<CollisionShape> colliders,
            MeshInstance mesh,
            Godot.Mesh itemMesh,
            IEnumerable<Marker> markers,
            IEnumerable<IMorphGroup> morphGroups,
            RigidBody node,
            ILoggerFactory loggerFactory) : base(node, loggerFactory)
        {
            Ensure.That(key, nameof(key)).IsNotNullOrEmpty();
            Ensure.That(displayName, nameof(displayName)).IsNotNullOrEmpty();
            Ensure.That(mesh, nameof(mesh)).IsNotNull();
            Ensure.That(itemMesh, nameof(itemMesh)).IsNotNull();
            Ensure.That(markers, nameof(markers)).IsNotNull();
            Ensure.That(morphGroups, nameof(morphGroups)).IsNotNull();

            Colliders = colliders.Freeze();

            Ensure.Enumerable.HasItems(Colliders, nameof(colliders));

            Key = key;
            DisplayName = displayName;
            Description = description;
            EquipmentType = equipmentType;
            Mesh = mesh;
            ItemMesh = itemMesh;
            Markers = markers.ToMap();

            _configurations = configurations?.Freeze();

            Ensure.Enumerable.HasItems(_configurations, nameof(configurations));

            Configurations = _configurations.ToMap();

            _configurations.ToObservable()
                .Select(c => c.OnActiveStateChange.Where(identity).Select(_ => c)).Switch()
                .Select(active => _configurations.Where(c => c != active && c.Active).ToObservable()).Switch()
                .TakeUntil(Disposed.Where(identity))
                .Subscribe(c => c.Deactivate(), this);

            _labelMarker = this.FindLabelMarker();

            var groups = morphGroups.Freeze();
            var morphs = groups.Flatten().Map(d => d.CreateMorph(this, LoggerFactory)).Freeze();

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                morphs.Iter(m => this.LogDebug("Found morph '{}'.", m));
            }

            Morphs = new MorphSet(groups, morphs);
        }

        protected override void PostConstruct()
        {
            base.PostConstruct();

            UpdateEquipState(Equipped);
        }

        public virtual void Equip(IEquipmentHolder holder)
        {
            UpdateEquipState(true);

            Configuration.OnEquip(holder, this);
        }

        public virtual void Unequip(IEquipmentHolder holder)
        {
            Configuration.OnUnequip(holder, this);

            UpdateEquipState(false);
        }

        private void UpdateEquipState(bool equipped)
        {
            this.LogDebug("Equipped state has changed: {}.", equipped);

            Node.Mode = equipped ? RigidBody.ModeEnum.Kinematic : RigidBody.ModeEnum.Rigid;
            Node.Sleeping = equipped;
            Node.InputRayPickable = !equipped;

            Colliders.Iter(c => c.Disabled = equipped);
        }

        public bool AllowedFor(ISlotContainer context) => true;

        public bool AllowedFor(object context) => true;
    }
}
