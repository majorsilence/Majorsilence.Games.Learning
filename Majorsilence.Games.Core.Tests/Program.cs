using Majorsilence.Games.Core.Tests;

// Pure-logic tests first (no SDL, can't be blocked by display quirks), the
// windowed render test last.
var levelLoaderTest = new LevelLoaderTest();
levelLoaderTest.Test1();

var platformerBodyTest = new PlatformerBodyTest();
platformerBodyTest.Test1();

var renderTest = new RenderTest();
renderTest.Test1();