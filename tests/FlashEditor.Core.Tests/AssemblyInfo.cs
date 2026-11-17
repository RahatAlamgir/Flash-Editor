// Tests share one global object renderer (ObjectRendering.Renderer) and install different fake ones, so test
// classes must not run at the same time. The whole suite takes only a few seconds.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
