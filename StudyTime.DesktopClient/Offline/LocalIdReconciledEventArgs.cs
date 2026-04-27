namespace StudyTime.DesktopClient.Offline
{
    /// <summary>
    /// Outbox senkronu sonrası yerel geçici Id'nin sunucu Id'si ile değiştiğini bildirir.
    /// UI (Blazor) bu olaya abone olarak rotayı, seçili öğeyi veya <c>StateHasChanged</c> güncelleyebilir.
    /// </summary>
    public sealed class LocalIdReconciledEventArgs : EventArgs
    {
        /// <summary><c>Task</c> | <c>Lesson</c> | <c>StudySession</c></summary>
        public required string EntityType { get; init; }

        /// <summary>İstemcinin ürettiği geçici Id (veya çalışma oturumu için yerel oturum Id'si).</summary>
        public required Guid TempId { get; init; }

        /// <summary>API'nin atadığı kalıcı Id.</summary>
        public required Guid ServerId { get; init; }
    }
}
