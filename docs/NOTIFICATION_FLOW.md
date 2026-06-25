# Notification Flow

Bu dokuman, mevcut bildirim altyapisini kullanici senaryosu bazli akislarla gosterir.

## 1) Timer Bitisinden Bildirim Uretimine Akis

```mermaid
flowchart TD
    A[User: Timer baslatti] --> B[GlobalTimerService calisir]
    B --> C{Timer/Break bitti mi?}
    C -- Evet --> D[TimerNotificationService event yakalar]
    D --> E[AppNotificationCenterService.SendNotificationAsync]

    E --> F[SyncedNotificationApiService.CreateAndSaveOfflineAsync]
    F --> G[LocalNotificationCache: NotificationCacheEntry yaz]
    G --> H{ConnectivityService.IsOnline?}

    H -- Online --> I[NotificationApiService POST api/notification]
    I --> J{Server Id dondu mu?}
    J -- Evet --> K[ReconcileIdAsync localId -> serverId]
    J -- Hayir --> L[localId ile devam]

    H -- Offline/API fail --> M[OutboxProcessor.Enqueue Notification/Create]

    K --> N[UI listesine ekle + unread++]
    L --> N
    M --> N

    N --> O{App foreground mu?}
    O -- Evet --> P[Sadece UI event: OnNewNotification]
    O -- Hayir --> Q[IPlatformNotificationHandler.ShowOSNotification]
    Q --> R{Platform}
    R -- Windows --> S[WindowsTrayNotificationHandler -> TrayIcon balloon]
    R -- Mobile --> T[MobilePushNotificationHandler stub - TODO]
```

## 2) Bildirimi Okundu Yapma ve Hepsini Okuma

```mermaid
sequenceDiagram
    autonumber
    participant U as User
    participant UI as NotificationCenter.razor
    participant APP as AppNotificationCenterService
    participant SYNC as SyncedNotificationApiService
    participant CACHE as LocalNotificationCache
    participant API as NotificationController/API

    U->>UI: Bildirime tikla
    UI->>APP: MarkAsReadAsync(id)
    APP->>SYNC: MarkAsReadAsync(id)
    SYNC->>CACHE: UpdateReadStatusAsync(id,true)
    SYNC->>API: PUT /api/notification/{id}/read (online ise)
    API-->>SYNC: 204 NoContent
    SYNC-->>APP: done
    APP-->>UI: OnNotificationsChanged (unread azalir)

    U->>UI: Hepsini oku
    UI->>APP: MarkAllAsReadAsync()
    APP->>SYNC: MarkAllAsReadAsync()
    SYNC->>CACHE: MarkAllAsReadAsync()
    SYNC->>API: PUT /api/notification/read-all (online ise)
    API-->>SYNC: 204 NoContent
    APP-->>UI: listeyi read yap + refresh
```

## 3) Offline Outbox Replay ve Id Uzlastirma

```mermaid
flowchart TD
    A[Senaryo: Offline bildirim olustu] --> B[OutboxQueue: Notification/Create]
    B --> C{Internet geri geldi mi?}
    C -- Evet --> D[OutboxProcessor.FlushAsync]
    D --> E[Replay Notification/Create]
    E --> F[API POST /api/notification]
    F --> G{ServerId != localId ?}
    G -- Evet --> H[TempIdMap yaz + LocalNotificationCache.ReconcileIdAsync]
    H --> I[LocalIdReconciled event]
    G -- Hayir --> J[Ek islem yok]
    I --> K[Outbox entry sil]
    J --> K
    K --> L[Sistem tutarli: local/server id hizali]
```

