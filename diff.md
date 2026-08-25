diff --git a/EmpireIdle.Api.Tests/Concurrency/OptimisticLockingTests.cs b/EmpireIdle.Api.Tests/Concurrency/OptimisticLockingTests.cs
index 4687366..c6535f6 100644
--- a/EmpireIdle.Api.Tests/Concurrency/OptimisticLockingTests.cs
+++ b/EmpireIdle.Api.Tests/Concurrency/OptimisticLockingTests.cs
@@ -44,8 +44,8 @@ public class OptimisticLockingTests : IAsyncLifetime
         var garrisonA = await LoadAsync(contextA, garrisonId);
         var garrisonB = await LoadAsync(contextB, garrisonId);
 
-        garrisonA.SendUnits(new Dictionary<string, int> { ["infantry"] = 10 });
-        garrisonB.SendUnits(new Dictionary<string, int> { ["infantry"] = 10 });
+        garrisonA.SendUnits(new Dictionary<string, int> { ["infantry"] = 10 }, DateTime.UtcNow);
+        garrisonB.SendUnits(new Dictionary<string, int> { ["infantry"] = 10 }, DateTime.UtcNow);
 
         await contextA.SaveChangesAsync();
 
@@ -75,8 +75,8 @@ public class OptimisticLockingTests : IAsyncLifetime
 
         // SendUnits міняє тільки VillageUnit.Count — рядок Garrisons
         // оновиться лише завдяки Touch()
-        garrisonA.SendUnits(new Dictionary<string, int> { ["infantry"] = 3 });
-        garrisonB.SendUnits(new Dictionary<string, int> { ["infantry"] = 4 });
+        garrisonA.SendUnits(new Dictionary<string, int> { ["infantry"] = 3 }, DateTime.UtcNow);
+        garrisonB.SendUnits(new Dictionary<string, int> { ["infantry"] = 4 }, DateTime.UtcNow);
 
         await contextA.SaveChangesAsync();
 
@@ -159,7 +159,7 @@ public class OptimisticLockingTests : IAsyncLifetime
         await using var context = CreateContext();
 
         var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid());
-        garrison.ReceiveUnits(new Dictionary<string, int> { ["infantry"] = infantry });
+        garrison.ReceiveUnits(new Dictionary<string, int> { ["infantry"] = infantry }, DateTime.UtcNow);
 
         context.Garrisons.Add(garrison);
         await context.SaveChangesAsync();
diff --git a/EmpireIdle.Architecture.Tests/ClockDisciplineTests.cs b/EmpireIdle.Architecture.Tests/ClockDisciplineTests.cs
index b2e7c86..84eb7c2 100644
--- a/EmpireIdle.Architecture.Tests/ClockDisciplineTests.cs
+++ b/EmpireIdle.Architecture.Tests/ClockDisciplineTests.cs
@@ -20,7 +20,7 @@ public class ClockDisciplineTests
     /// Останнє вимірювання: 60. Зріз Villages лічильник не зрушив — виклики
     /// переїхали з домену в хендлери. Наступний зріз: доменні події (−10).
     /// </summary>
-    private const int AllowedDirectClockCalls = 20;
+    private const int AllowedDirectClockCalls = 11;
 
     private static readonly Regex DirectClockCall =
         new(@"\bDateTime\s*\.\s*(UtcNow|Now|Today)\b", RegexOptions.Compiled);
diff --git a/EmpireIdle.Domain.Tests/Entities/BuildingTests.cs b/EmpireIdle.Domain.Tests/Entities/BuildingTests.cs
index 86cef7c..fd9fdd5 100644
--- a/EmpireIdle.Domain.Tests/Entities/BuildingTests.cs
+++ b/EmpireIdle.Domain.Tests/Entities/BuildingTests.cs
@@ -10,7 +10,7 @@ public class BuildingTests
     // farm: 10/хв, кап 60, ріст капу 1.3
     private static readonly BuildingConfig Farm = TestData.FarmConfigs()["farm"];
 
-    private static Building CreateFarm() => new(Guid.NewGuid(), Guid.NewGuid(), "farm");
+    private static Building CreateFarm() => new(Guid.NewGuid(), Guid.NewGuid(), "farm", DateTime.UtcNow);
 
     /// <summary>Піднімає рівень через реальний шлях: почати → завершити.</summary>
     private static void RaiseLevel(Building building, int times, DateTime utcNow)
@@ -199,7 +199,7 @@ public class BuildingTests
     public void StoredAt_ShouldReturnZero_ForNonProducingBuilding()
     {
         var config = new BuildingConfig { Key = "townhall", ProducesResource = null, BaseStorage = 0 };
-        var building = new Building(Guid.NewGuid(), Guid.NewGuid(), "townhall");
+        var building = new Building(Guid.NewGuid(), Guid.NewGuid(), "townhall", DateTime.UtcNow);
 
         Assert.Equal(0, building.StoredAt(config, building.LastAccruedAt.AddHours(10), ProductionBoost.None));
     }
diff --git a/EmpireIdle.Domain.Tests/Entities/GarrisonTests.cs b/EmpireIdle.Domain.Tests/Entities/GarrisonTests.cs
index 76ba48e..3fd6ad6 100644
--- a/EmpireIdle.Domain.Tests/Entities/GarrisonTests.cs
+++ b/EmpireIdle.Domain.Tests/Entities/GarrisonTests.cs
@@ -116,7 +116,7 @@ namespace EmpireIdle.Domain.Tests.Entities
             garrison.TrainUnits("infantry", 5, 5, 100, TimeSpan.FromMinutes(10), DateTime.UtcNow);
             garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(11));
 
-            garrison.SendUnits(new Dictionary<string, int> { ["infantry"] = 3 });
+            garrison.SendUnits(new Dictionary<string, int> { ["infantry"] = 3 }, DateTime.UtcNow);
 
             Assert.Equal(2, garrison.Units.Single().Count);
         }
@@ -130,7 +130,7 @@ namespace EmpireIdle.Domain.Tests.Entities
             garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(5));
 
             Assert.Throws<NotEnoughResourcesException>(() =>
-                garrison.SendUnits(new Dictionary<string, int> { ["infantry"] = 5 }));
+                garrison.SendUnits(new Dictionary<string, int> { ["infantry"] = 5 }, DateTime.UtcNow));
 
             Assert.Equal(2, garrison.Units.Single().Count); // нічого не зняли
         }
@@ -144,8 +144,8 @@ namespace EmpireIdle.Domain.Tests.Entities
             garrison.CompleteDueTraining(DateTime.UtcNow.AddMinutes(11));
 
             var army = new Dictionary<string, int> { ["infantry"] = 3 };
-            garrison.SendUnits(army);
-            garrison.ReceiveUnits(army);
+            garrison.SendUnits(army, DateTime.UtcNow);
+            garrison.ReceiveUnits(army, DateTime.UtcNow);
 
             Assert.Equal(5, garrison.Units.Single().Count); // усі повернулись
         }
diff --git a/src/EmpireIdle.Application/Garrisons/Commands/HealWoundedCommand.cs b/src/EmpireIdle.Application/Garrisons/Commands/HealWoundedCommand.cs
index 8d09131..8de69c9 100644
--- a/src/EmpireIdle.Application/Garrisons/Commands/HealWoundedCommand.cs
+++ b/src/EmpireIdle.Application/Garrisons/Commands/HealWoundedCommand.cs
@@ -67,7 +67,7 @@ namespace EmpireIdle.Application.Garrisons.Commands
             var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                 ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");
 
-            var healed = garrison.HealWounded(request.Units);
+            var healed = garrison.HealWounded(request.Units, now);
             if (healed.Count == 0)
                 throw new InvalidStateException("Nothing to heal.");
 
diff --git a/src/EmpireIdle.Application/Garrisons/Commands/SpeedUpTrainingCommand.cs b/src/EmpireIdle.Application/Garrisons/Commands/SpeedUpTrainingCommand.cs
index fa8d99b..28a2d66 100644
--- a/src/EmpireIdle.Application/Garrisons/Commands/SpeedUpTrainingCommand.cs
+++ b/src/EmpireIdle.Application/Garrisons/Commands/SpeedUpTrainingCommand.cs
@@ -68,7 +68,7 @@ namespace EmpireIdle.Application.Garrisons.Commands
                 wallet.SpendGems(new GemAmount(cost), $"Speed up training of {order.UnitType}", request.PlayerId, now);
             }
 
-            garrison.ReduceTrainingTime(order.Id, order.CompletesAt - now);
+            garrison.ReduceTrainingTime(order.Id, order.CompletesAt - now, now);
             garrison.CompleteDueTraining(now);
 
             await _unitOfWork.SaveChangesAsync(cancellationToken);
diff --git a/src/EmpireIdle.Application/Map/Commands/SpawnMonstersCommand.cs b/src/EmpireIdle.Application/Map/Commands/SpawnMonstersCommand.cs
index 5d22060..301d91c 100644
--- a/src/EmpireIdle.Application/Map/Commands/SpawnMonstersCommand.cs
+++ b/src/EmpireIdle.Application/Map/Commands/SpawnMonstersCommand.cs
@@ -17,6 +17,7 @@ namespace EmpireIdle.Application.Map.Commands
         private readonly IMapRepository _mapRepository;
         private readonly IUnitOfWork _unitOfWork;
         private readonly MonsterSpawner _spawner;
+        private readonly TimeProvider _timeProvider;
         private readonly ILogger<SpawnMonstersCommandHandler> _logger;
 
         public SpawnMonstersCommandHandler(
@@ -24,17 +25,20 @@ namespace EmpireIdle.Application.Map.Commands
             IMapRepository mapRepository,
             IUnitOfWork unitOfWork,
             MonsterSpawner spawner,
+            TimeProvider timeProvider,
             ILogger<SpawnMonstersCommandHandler> logger)
         {
             _monsterRepository = monsterRepository;
             _mapRepository = mapRepository;
             _unitOfWork = unitOfWork;
             _spawner = spawner;
+            _timeProvider = timeProvider;
             _logger = logger;
         }
 
         public async Task Handle(SpawnMonstersCommand request, CancellationToken cancellationToken)
         {
+            var now = _timeProvider.GetUtcNow().DateTime;
             var current = await _monsterRepository.CountAsync(request.ServerId, cancellationToken);
             var target = _spawner.GetTargetPopulation();
             var missing = Math.Min(target - current, MaxSpawnsPerRun);
@@ -60,7 +64,7 @@ namespace EmpireIdle.Application.Map.Commands
                 var (type, level, x, y) = spot.Value;
                 reserved.Add((x, y));
 
-                var monster = new Monster(Guid.NewGuid(), request.ServerId, type, level, x, y);
+                var monster = new Monster(Guid.NewGuid(), request.ServerId, type, level, x, y, now);
 
                 await _monsterRepository.AddAsync(monster, cancellationToken);
                 await _mapRepository.AddAsync(
diff --git a/src/EmpireIdle.Application/Marches/Commands/CompleteMarchCommand.cs b/src/EmpireIdle.Application/Marches/Commands/CompleteMarchCommand.cs
index 84c1f46..8a45e29 100644
--- a/src/EmpireIdle.Application/Marches/Commands/CompleteMarchCommand.cs
+++ b/src/EmpireIdle.Application/Marches/Commands/CompleteMarchCommand.cs
@@ -90,7 +90,7 @@ namespace EmpireIdle.Application.Marches.Commands
 
                 var survivors = march.GetUnits();
                 if (survivors.Count > 0)
-                    garrison.ReceiveUnits(survivors);
+                    garrison.ReceiveUnits(survivors, now);
 
                 march.Complete(now);
             }
@@ -139,8 +139,8 @@ namespace EmpireIdle.Application.Marches.Commands
 
             var split = _casualties.Split(result.AttackerLosses, woundedCapacity);
 
-            march.ApplyLosses(result.AttackerLosses);
-            garrison.AdmitWounded(split.Wounded);
+            march.ApplyLosses(result.AttackerLosses, utcNow);
+            garrison.AdmitWounded(split.Wounded, utcNow);
 
             if (result.AttackerWon)
             {
@@ -179,7 +179,7 @@ namespace EmpireIdle.Application.Marches.Commands
             if (split.Recoverable.Count > 0)
             {
                 var expiresAt = utcNow.AddHours(_combatConfig.RecoveryWindowHours);
-                garrison.AddRecoverable(split.Recoverable, report.Id, expiresAt);
+                garrison.AddRecoverable(split.Recoverable, report.Id, expiresAt, utcNow);
             }
 
             march.RecordBattle(village.PlayerId, report.Id, result.AttackerWon, report.TargetName, utcNow);
diff --git a/src/EmpireIdle.Application/Marches/Commands/SendMarchCommand.cs b/src/EmpireIdle.Application/Marches/Commands/SendMarchCommand.cs
index 9f8d824..862edb0 100644
--- a/src/EmpireIdle.Application/Marches/Commands/SendMarchCommand.cs
+++ b/src/EmpireIdle.Application/Marches/Commands/SendMarchCommand.cs
@@ -75,7 +75,7 @@ namespace EmpireIdle.Application.Marches.Commands
             var (targetX, targetY) = await ResolveTargetAsync(request, cancellationToken);
 
             // Знімаємо юнітів із гарнізону (перевірки наявності — всередині)
-            garrison.SendUnits(request.Units);
+            garrison.SendUnits(request.Units, now);
 
             var duration = _calculator.CalculateDuration(
                 _serverContext.ServerId, village.X, village.Y, targetX, targetY, request.Units);
diff --git a/src/EmpireIdle.Application/Marches/Commands/SpeedUpMarchCommand.cs b/src/EmpireIdle.Application/Marches/Commands/SpeedUpMarchCommand.cs
index 13a2ef9..8be805e 100644
--- a/src/EmpireIdle.Application/Marches/Commands/SpeedUpMarchCommand.cs
+++ b/src/EmpireIdle.Application/Marches/Commands/SpeedUpMarchCommand.cs
@@ -80,7 +80,7 @@ namespace EmpireIdle.Application.Marches.Commands
             }
 
             // Зсуваємо прибуття на «зараз»; бій або повернення відпрацює сканер
-            march.ReduceTravelTime(march.ArrivesAt - now);
+            march.ReduceTravelTime(march.ArrivesAt - now, now);
 
             await _unitOfWork.SaveChangesAsync(cancellationToken);
 
diff --git a/src/EmpireIdle.Application/Players/Commands/CreatePlayerCommand.cs b/src/EmpireIdle.Application/Players/Commands/CreatePlayerCommand.cs
index ea758a1..497cd23 100644
--- a/src/EmpireIdle.Application/Players/Commands/CreatePlayerCommand.cs
+++ b/src/EmpireIdle.Application/Players/Commands/CreatePlayerCommand.cs
@@ -67,7 +67,7 @@ namespace EmpireIdle.Application.Players.Commands
 
             var playerId = Guid.NewGuid();
 
-            var player = new Player(playerId, request.UserName, email, request.UserId, serverId);
+            var player = new Player(playerId, request.UserName, email, request.UserId, now, serverId);
             var wallet = new PlayerWallet(Guid.NewGuid(), request.UserId);
             var (x, y) = await _settlementPlacer.FindSpotAsync(
                 serverId: serverId,
diff --git a/src/EmpireIdle.Domain/Entities/Building.cs b/src/EmpireIdle.Domain/Entities/Building.cs
index 96e62a8..bc5f3b9 100644
--- a/src/EmpireIdle.Domain/Entities/Building.cs
+++ b/src/EmpireIdle.Domain/Entities/Building.cs
@@ -37,13 +37,13 @@ namespace EmpireIdle.Domain.Entities
         /// <summary>Чи триває апгрейд будівлі (виробництво на цей час зупинене).</summary>
         public bool IsUnderConstruction => ConstructionCompletesAt is not null;
 
-        public Building(Guid id, Guid villageId, string type) : base(id)
+        public Building(Guid id, Guid villageId, string type, DateTime utcNow) : base(id)
         {
             VillageId = villageId;
             Type = type;
             Level = BuildingLevel.Initial;
-            LastCollectedAt = DateTime.UtcNow;
-            LastAccruedAt = DateTime.UtcNow;
+            LastCollectedAt = utcNow;
+            LastAccruedAt = utcNow;
         }
 
         protected Building() { } // Для EF Core
diff --git a/src/EmpireIdle.Domain/Entities/Garrison.cs b/src/EmpireIdle.Domain/Entities/Garrison.cs
index a1e5b58..95c849d 100644
--- a/src/EmpireIdle.Domain/Entities/Garrison.cs
+++ b/src/EmpireIdle.Domain/Entities/Garrison.cs
@@ -90,7 +90,7 @@ namespace EmpireIdle.Domain.Entities
             }
 
             if (due.Count > 0)
-                Touch();
+                Touch(utcNow);
 
             return due.Count;
         }
@@ -99,7 +99,7 @@ namespace EmpireIdle.Domain.Entities
         /// Знімає юнітів із гарнізону для походу.
         /// </summary>
         /// <param name="units">Тип юніта → кількість.</param>
-        public void SendUnits(IReadOnlyDictionary<string, int> units)
+        public void SendUnits(IReadOnlyDictionary<string, int> units, DateTime utcNow)
         {
             if (units.Count == 0)
                 throw new RequirementNotMetException("Cannot send an empty army.");
@@ -120,11 +120,11 @@ namespace EmpireIdle.Domain.Entities
             foreach (var (unitType, count) in units)
                 _units.First(u => u.UnitType == unitType).Subtract(count);
 
-            Touch();
+            Touch(utcNow);
         }
 
         /// <summary>Повертає юнітів у гарнізон (після походу).</summary>
-        public void ReceiveUnits(IReadOnlyDictionary<string, int> units)
+        public void ReceiveUnits(IReadOnlyDictionary<string, int> units, DateTime utcNow)
         {
             foreach (var (unitType, count) in units)
             {
@@ -139,11 +139,11 @@ namespace EmpireIdle.Domain.Entities
                 }
                 unit.Add(count);
             }
-            Touch();
+            Touch(utcNow);
         }
 
         /// <summary>Приймає поранених після бою (у межах вільної місткості).</summary>
-        public void AdmitWounded(IReadOnlyDictionary<string, int> wounded)
+        public void AdmitWounded(IReadOnlyDictionary<string, int> wounded, DateTime utcNow)
         {
             foreach (var (unitType, count) in wounded)
             {
@@ -158,13 +158,13 @@ namespace EmpireIdle.Domain.Entities
                 }
                 stack.Add(count);
             }
-            Touch();
+            Touch(utcNow);
         }
 
         /// <summary>
         /// Виліковує поранених: вони повертаються в гарнізон.
         /// </summary>
-        public Dictionary<string, int> HealWounded(IReadOnlyDictionary<string, int> toHeal)
+        public Dictionary<string, int> HealWounded(IReadOnlyDictionary<string, int> toHeal, DateTime utcNow)
         {
             var healed = new Dictionary<string, int>();
 
@@ -182,28 +182,27 @@ namespace EmpireIdle.Domain.Entities
             _wounded.RemoveAll(w => w.Count <= 0);
 
             if (healed.Count > 0)
-                ReceiveUnits(healed);
+                ReceiveUnits(healed, utcNow);
 
-            Touch();
+            Touch(utcNow);
             return healed;
         }
 
         /// <summary>Прискорює замовлення тренування (speedup за gems).</summary>
-        public void ReduceTrainingTime(Guid orderId, TimeSpan reduction)
+        public void ReduceTrainingTime(Guid orderId, TimeSpan reduction, DateTime utcNow)
         {
             var order = _trainingOrders.FirstOrDefault(o => o.Id == orderId)
                  ?? throw new EntityNotFoundException("Training order", orderId);
 
             order.Reduce(reduction);
-            Touch();
+            Touch(utcNow);
         }
 
         /// <summary>Скільки юнітів зараз доступно для викупу.</summary>
         public int RecoverableCount(DateTime utcNow) => _recoverable.Where(r => r.IsActive(utcNow)).Sum(r => r.Count);
 
         /// <summary>Записує відновлюваних після бою — окремим стеком зі своїм дедлайном.</summary>
-        public void AddRecoverable(IReadOnlyDictionary<string, int> units,
-            Guid battleReportId, DateTime expiresAt)
+        public void AddRecoverable(IReadOnlyDictionary<string, int> units, Guid battleReportId, DateTime expiresAt, DateTime utcNow)
         {
             foreach (var (unitType, count) in units)
             {
@@ -212,7 +211,7 @@ namespace EmpireIdle.Domain.Entities
 
                 _recoverable.Add(new RecoverableUnit(Guid.NewGuid(), Id, battleReportId, unitType, count, expiresAt));
             }
-            Touch();
+            Touch(utcNow);
         }
 
         /// <summary>
@@ -251,12 +250,12 @@ namespace EmpireIdle.Domain.Entities
             _recoverable.RemoveAll(r => r.Count <= 0);
 
             if (recovered.Count > 0)
-                ReceiveUnits(recovered);
+                ReceiveUnits(recovered, utcNow);
 
-            Touch();
+            Touch(utcNow);
             return recovered;
         }
 
-        private void Touch() => UpdatedAt = DateTime.UtcNow;
+        private void Touch(DateTime utcNow) => UpdatedAt = utcNow;
     }
 }
diff --git a/src/EmpireIdle.Domain/Entities/March.cs b/src/EmpireIdle.Domain/Entities/March.cs
index 10811df..251ea98 100644
--- a/src/EmpireIdle.Domain/Entities/March.cs
+++ b/src/EmpireIdle.Domain/Entities/March.cs
@@ -95,7 +95,7 @@ namespace EmpireIdle.Domain.Entities
             State = MarchState.Returning;
             ArrivesAt = utcNow + returnDuration;
 
-            Touch();
+            Touch(utcNow);
         }
 
         /// <summary>Армія повернулася додому — похід завершено.</summary>
@@ -107,14 +107,14 @@ namespace EmpireIdle.Domain.Entities
             State = MarchState.Completed;
             RaiseDomainEvent(new Events.MarchReturned(Id, GarrisonId, utcNow));
 
-            Touch();
+            Touch(utcNow);
         }
 
         /// <summary>
         /// Застосовує втрати після бою: зменшує склад армії.
         /// Загони, що загинули повністю, видаляються.
         /// </summary>
-        public void ApplyLosses(IReadOnlyDictionary<string, int> losses)
+        public void ApplyLosses(IReadOnlyDictionary<string, int> losses, DateTime utcNow)
         {
             foreach (var (unitType, lost) in losses)
             {
@@ -125,24 +125,24 @@ namespace EmpireIdle.Domain.Entities
             }
             _units.RemoveAll(u => u.Count <= 0);
 
-            Touch();
+            Touch(utcNow);
         }
 
         /// <summary>Фіксує факт бою для сповіщення гравця.</summary>
         public void RecordBattle(Guid playerId, Guid reportId, bool won, string targetName, DateTime utcNow)
         {
             RaiseDomainEvent(new Events.BattleFought(GarrisonId, playerId, Id, reportId, won, targetName, utcNow));
-            Touch();
+            Touch(utcNow);
         }
 
         /// <summary>Прискорює прибуття (speedup за gems).</summary>
-        public void ReduceTravelTime(TimeSpan reduction)
+        public void ReduceTravelTime(TimeSpan reduction, DateTime utcNow)
         {
             ArrivesAt -= reduction;
-            Touch();
+            Touch(utcNow);
         }
 
-        private void Touch() => UpdatedAt = DateTime.UtcNow;
+        private void Touch(DateTime utcNow) => UpdatedAt = utcNow;
     }
 
     /// <summary>Загін у складі походу.</summary>
diff --git a/src/EmpireIdle.Domain/Entities/Monster.cs b/src/EmpireIdle.Domain/Entities/Monster.cs
index 4521504..dcc0791 100644
--- a/src/EmpireIdle.Domain/Entities/Monster.cs
+++ b/src/EmpireIdle.Domain/Entities/Monster.cs
@@ -22,14 +22,14 @@ namespace EmpireIdle.Domain.Entities
         /// <summary>Коли з'явився (для аналітики й майбутнього деспавну).</summary>
         public DateTime SpawnedAt { get; private set; }
 
-        public Monster(Guid id, int serverId, string type, int level, int x, int y) : base(id)
+        public Monster(Guid id, int serverId, string type, int level, int x, int y, DateTime utcNow) : base(id)
         {
             ServerId = serverId;
             Type = type;
             Level = level;
             X = x;
             Y = y;
-            SpawnedAt = DateTime.UtcNow;
+            SpawnedAt = utcNow;
         }
 
         protected Monster() { } // Для EF Core
diff --git a/src/EmpireIdle.Domain/Entities/Player.cs b/src/EmpireIdle.Domain/Entities/Player.cs
index 8608fcd..40c0f19 100644
--- a/src/EmpireIdle.Domain/Entities/Player.cs
+++ b/src/EmpireIdle.Domain/Entities/Player.cs
@@ -23,12 +23,12 @@ namespace EmpireIdle.Domain.Entities
         /// <summary>Дата реєстрації.</summary>
         public DateTime CreatedAt { get; private set; }
 
-        public Player(Guid id, string username, string email, string userId, int serverId = 1) : base(id)
+        public Player(Guid id, string username, string email, string userId, DateTime utcNow, int serverId = 1) : base(id)
         {
             UserId = userId;
             Username = username;
             Email = email;
-            CreatedAt = DateTime.UtcNow;
+            CreatedAt = utcNow;
             ServerId = serverId;
         }
 
diff --git a/src/EmpireIdle.Domain/Entities/PlayerWallet.cs b/src/EmpireIdle.Domain/Entities/PlayerWallet.cs
index 7efb4f0..dbc3501 100644
--- a/src/EmpireIdle.Domain/Entities/PlayerWallet.cs
+++ b/src/EmpireIdle.Domain/Entities/PlayerWallet.cs
@@ -49,11 +49,10 @@ public class PlayerWallet : Entity
     public void AddGems(GemAmount amount, string reference, Guid notifyPlayerId, DateTime utcNow)
     {
         GemBalance = GemBalance.Add(amount);
-        _transactions.Add(new WalletTransaction(
-            Guid.NewGuid(), Id, TransactionType.GemPurchase, amount.Value, reference));
+        _transactions.Add(new WalletTransaction( Guid.NewGuid(), Id, TransactionType.GemPurchase, amount.Value, reference, utcNow));
 
         RaiseDomainEvent(new GemsPurchased(notifyPlayerId, amount, GemBalance, utcNow));
-        Touch();
+        Touch(utcNow);
     }
 
     /// <summary>
@@ -65,13 +64,12 @@ public class PlayerWallet : Entity
     public void SpendGems(GemAmount amount, string description, Guid notifyPlayerId, DateTime utcNow)
     {
         GemBalance = GemBalance.Subtract(amount);
-        _transactions.Add(new WalletTransaction(
-            Guid.NewGuid(), Id, TransactionType.GemSpend, -amount.Value, description));
+        _transactions.Add(new WalletTransaction( Guid.NewGuid(), Id, TransactionType.GemSpend, -amount.Value, description, utcNow));
 
         RaiseDomainEvent(new GemsSpent(notifyPlayerId, amount, GemBalance, description, utcNow));
-        Touch();
+        Touch(utcNow);
     }
 
-    private void Touch() => UpdatedAt = DateTime.UtcNow;
+    private void Touch(DateTime utcNow) => UpdatedAt = utcNow;
 }
 
diff --git a/src/EmpireIdle.Domain/Entities/QuestProgress.cs b/src/EmpireIdle.Domain/Entities/QuestProgress.cs
index f08f9b9..523a2dd 100644
--- a/src/EmpireIdle.Domain/Entities/QuestProgress.cs
+++ b/src/EmpireIdle.Domain/Entities/QuestProgress.cs
@@ -54,7 +54,7 @@ namespace EmpireIdle.Domain.Entities
 
             Objective(objectiveIndex).Add(amount);
             TryComplete(utcNow);
-            Touch();
+            Touch(utcNow);
         }
 
         /// <summary>
@@ -68,7 +68,7 @@ namespace EmpireIdle.Domain.Entities
 
             Objective(objectiveIndex).RaiseTo(current);
             TryComplete(utcNow);
-            Touch();
+            Touch(utcNow);
         }
 
         /// <summary>Забрати нагороду. Ідемпотентно: повторний виклик нічого не робить.</summary>
@@ -80,7 +80,7 @@ namespace EmpireIdle.Domain.Entities
             State = QuestState.Claimed;
             ClaimedAt = utcNow;
 
-            Touch();
+            Touch(utcNow);
             return true;
         }
 
@@ -100,7 +100,7 @@ namespace EmpireIdle.Domain.Entities
             StartedAt = utcNow;
             CompletedAt = null;
             ClaimedAt = null;
-            Touch();
+            Touch(utcNow);
         }
 
         private QuestObjectiveProgress Objective(int index)
@@ -118,6 +118,6 @@ namespace EmpireIdle.Domain.Entities
             RaiseDomainEvent(new QuestCompleted(PlayerId, QuestKey, utcNow));
         }
 
-        private void Touch() => UpdatedAt = DateTime.UtcNow;
+        private void Touch(DateTime utcNow) => UpdatedAt = utcNow;
     }
 }
diff --git a/src/EmpireIdle.Domain/Entities/Village.cs b/src/EmpireIdle.Domain/Entities/Village.cs
index 1f944f7..7738e80 100644
--- a/src/EmpireIdle.Domain/Entities/Village.cs
+++ b/src/EmpireIdle.Domain/Entities/Village.cs
@@ -121,7 +121,7 @@ namespace EmpireIdle.Domain.Entities
             if (_buildings.Any(b => b.Type == buildingType))
                 throw new AlreadyExistsException("Building", buildingType);
 
-            var building = new Building(Guid.NewGuid(), Id, buildingType);
+            var building = new Building(Guid.NewGuid(), Id, buildingType, utcNow);
             _buildings.Add(building);
 
             Touch(utcNow);
diff --git a/src/EmpireIdle.Domain/Entities/WalletTransaction.cs b/src/EmpireIdle.Domain/Entities/WalletTransaction.cs
index 91e6c61..8d30163 100644
--- a/src/EmpireIdle.Domain/Entities/WalletTransaction.cs
+++ b/src/EmpireIdle.Domain/Entities/WalletTransaction.cs
@@ -24,14 +24,13 @@ namespace EmpireIdle.Domain.Entities
         /// <summary>Час транзакції.</summary>
         public DateTime CreatedAt { get; private set; }
 
-        public WalletTransaction(Guid id, Guid walletId, TransactionType type,
-            int amount, string reference) : base(id)
+        public WalletTransaction(Guid id, Guid walletId, TransactionType type, int amount, string reference, DateTime utcNow) : base(id)
         {
             WalletId = walletId;
             Type = type;
             Amount = amount;
             Reference = reference;
-            CreatedAt = DateTime.UtcNow;
+            CreatedAt = utcNow;
         }
 
         protected WalletTransaction() { } // для EF Core
