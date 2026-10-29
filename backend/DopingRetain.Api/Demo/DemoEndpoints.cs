using DopingRetain.Api.Data;
using DopingRetain.Api.Memory;
using DopingRetain.Api.Reviews;

namespace DopingRetain.Api.Demo;

/// <summary>
/// Demo reset and editor endpoints. They change or reveal demo data (answer keys included), so they are mapped
/// only when Demo:Enabled is true; otherwise the routes do not exist (404).
/// </summary>
public static class DemoEndpoints
{
    public const string EnabledKey = "Demo:Enabled";

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>(EnabledKey);

    public static void MapDemoEndpoints(this WebApplication app)
    {
        if (!IsEnabled(app.Configuration)) return;

        // One group at a time: there is deliberately no route that resets both groups.
        app.MapPost("/api/demo/groups/{group}/reset", async (
            string group, AppDbContext db, TimeProvider clock, RetentionModelOptions options, CancellationToken cancellationToken) =>
        {
            if (ParseGroup(group) is not { } demoGroup)
            {
                return Results.Json(new ApiError("group_not_found", $"Demo group '{group}' does not exist. Use 'yks' or 'sixth_grade'."),
                    statusCode: StatusCodes.Status404NotFound);
            }
            await DemoReset.ResetAsync(db, demoGroup, clock.GetUtcNow(), options, cancellationToken);
            return Results.NoContent();
        });

        app.MapGet("/api/demo/sixth-grade", (DemoEditorService editor, CancellationToken cancellationToken) =>
            editor.GetGroupAsync(cancellationToken));

        app.MapGet("/api/demo/students/{studentId}/topics/{topicId}/normal-test",
            async (string studentId, string topicId, DemoEditorService editor, CancellationToken cancellationToken) =>
                (await editor.GetNormalTestAsync(studentId, topicId, cancellationToken)).ToHttpResult());

        app.MapPut("/api/demo/students/{studentId}/topics/{topicId}/normal-test",
            async (string studentId, string topicId, SaveNormalTestRequest request, DemoEditorService editor, CancellationToken cancellationToken) =>
                (await editor.SaveNormalTestAsync(studentId, topicId, request, cancellationToken)).ToHttpResult());

        app.MapDelete("/api/demo/students/{studentId}/topics/{topicId}/history",
            async (string studentId, string topicId, DemoEditorService editor, CancellationToken cancellationToken) =>
                (await editor.ClearHistoryAsync(studentId, topicId, cancellationToken)).ToHttpResult());
    }

    /// <summary>The route names of the groups, matching their JSON values.</summary>
    public static DemoGroup? ParseGroup(string value) => value switch
    {
        "yks" => DemoGroup.Yks,
        "sixth_grade" => DemoGroup.SixthGrade,
        _ => null,
    };
}
