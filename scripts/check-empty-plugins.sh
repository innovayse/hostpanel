#!/usr/bin/env sh
#
# Fails when a built test project would start the host with a plugin already present.
#
# The host loads every plugin it finds in plugins/ and plugins-installed/ beside its own binaries
# (PluginOptions.BuiltInRoot and the default PluginOptions.InstallRoot). Under the test suites the
# binaries are the test project's output, so those two folders under its bin/ are what the host
# scans. The pipelines run this after the build and before the tests to prove that the whole suite
# is green with no plugin loaded: a build that started staging a plugin into a test output, or a
# leftover upload, would make the suite pass only because a plugin was there. A test that needs a
# plugin creates it itself and removes it afterwards.
#
# An absent folder and an empty one are both fine. Anything inside either is a failure.
#
# Usage, from the backend folder, once the test projects are built:
#   sh ../scripts/check-empty-plugins.sh tests/Innovayse.Hostpanel.Integration.Tests [<test project> ...]

set -eu

if [ "$#" -eq 0 ]; then
  echo "Usage: $0 <test project folder> [<test project folder> ...]" >&2
  exit 2
fi

status=0

for project in "$@"; do
  if [ ! -d "$project/bin" ]; then
    echo "check-empty-plugins: $project has no bin/ folder; build it before this check." >&2
    status=1
    continue
  fi

  # Every output folder of the project (any configuration, any target framework) is checked:
  # bin/<configuration>/<framework>/plugins and .../plugins-installed must hold nothing.
  entries=$(find "$project/bin" -mindepth 4 \
    \( -path "$project/bin/*/*/plugins/*" -o -path "$project/bin/*/*/plugins-installed/*" \) | head -n 20)

  if [ -n "$entries" ]; then
    echo "check-empty-plugins: a plugin folder of $project is not empty; the tests must start with no plugin:" >&2
    echo "$entries" >&2
    status=1
  else
    echo "check-empty-plugins: $project starts with no plugin."
  fi
done

exit "$status"
