#!/usr/bin/env bash
# Build-number guard check (Core standards/versioning.md rules 1-3, 7).
# Run from Git Bash:  bash tests/build-guard-check.sh
# No network. Temp dirs live under mktemp -d and are removed on exit.
#
# build.cmd is run as  cmd //c ".\\build.cmd"  (bare "build.cmd" fails here:
# NoDefaultCurrentDirectoryInExePath=1). Output is matched on ASCII only; the
# refusal message for a shallow clone must contain the word "shallow".
#
# Copies of the tree (clones, exports) get the CURRENT build.cmd from the code
# root laid over them, so uncommitted changes to build.cmd are what is tested.
# Never runs tests/run-tests.cmd. The exe from the normal build is kept.

unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE   # a hook or IDE task must not point the test at the real repository

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
FAILS=0
fail() { echo "FAIL: $*"; FAILS=$((FAILS + 1)); }

T=$(mktemp -d)
cleanup() { chmod -R u+w "$T" 2>/dev/null; rm -rf "$T"; }
trap cleanup EXIT

SHA=$(git -C "$ROOT" rev-parse HEAD) || { echo "FAIL: no git HEAD in $ROOT"; exit 1; }
COMMON=$(cd "$ROOT" && git rev-parse --git-common-dir)
COMMON=$(cd "$ROOT" && cd "$COMMON" && pwd)    # repository the worktree belongs to

# run_build <dir> [env assignments...] -> sets RC and OUT
run_build() {
  local dir=$1; shift
  OUT=$(cd "$dir" && env "$@" cmd //c ".\\build.cmd" 2>&1 </dev/null)
  RC=$?
}

# full_clone <name> -> full (non-shallow) clone at the worktree's HEAD, current build.cmd laid over
full_clone() {
  local d="$T/$1"
  git clone -q --no-checkout "file://$COMMON" "$d" >/dev/null 2>&1 &&
    git -C "$d" checkout -q "$SHA" >/dev/null 2>&1 || { fail "setup: full clone $1"; return 1; }
  cp "$ROOT/build.cmd" "$d/build.cmd"
}

compiled() { [ -e "$1/DIIISPL.exe" ] || [ -e "$1/BuildInfo.g.cs" ]; }

# ---- 1. normal build in the code root -------------------------------------
run_build "$ROOT"
EXPECT=$(git -C "$ROOT" rev-list --count HEAD)
GOT=$(grep -a -o 'Number = "[^"]*"' "$ROOT/BuildInfo.g.cs" 2>/dev/null | sed 's/.*"\(.*\)"/\1/')
[ "$RC" -eq 0 ] || fail "1: normal build rc=$RC, expected 0"
[ -n "$GOT" ] && [ "$GOT" = "$EXPECT" ] || fail "1: BuildInfo number '$GOT' != git rev-list --count HEAD '$EXPECT'"

# ---- 2. shallow clone ------------------------------------------------------
S="$T/sclone"
mkdir "$S" && git -C "$S" init -q &&
  git -C "$S" fetch -q --depth 1 "file://$COMMON" "$SHA" >/dev/null 2>&1 &&
  git -C "$S" checkout -q FETCH_HEAD >/dev/null 2>&1 || fail "setup: shallow clone"
if [ "$(git -C "$S" rev-parse --is-shallow-repository 2>/dev/null)" != "true" ]; then
  fail "setup: clone is not shallow"
else
  cp "$ROOT/build.cmd" "$S/build.cmd"
  run_build "$S"
  [ "$RC" -ne 0 ] || fail "2: shallow clone built with rc=0"
  echo "$OUT" | grep -aiq 'shallow' || fail "2: output does not name the shallow clone"
  compiled "$S" && fail "2: shallow clone produced DIIISPL.exe/BuildInfo.g.cs (not refused before compiling)"
fi

# ---- 3. GIT_DIR / GIT_WORK_TREE set ---------------------------------------
if full_clone gitenv; then
  G="$T/gitenv"
  run_build "$G" "GIT_DIR=$T/no-such.git"
  [ "$RC" -ne 0 ] || fail "3: GIT_DIR=no-such.git built with rc=0"
  compiled "$G" && fail "3: GIT_DIR set, yet compiled (not refused before compiling)"
  rm -f "$G/DIIISPL.exe" "$G/BuildInfo.g.cs"

  run_build "$G" "GIT_WORK_TREE=$G"
  [ "$RC" -ne 0 ] || fail "3: GIT_WORK_TREE set built with rc=0"
  compiled "$G" && fail "3: GIT_WORK_TREE set, yet compiled (not refused before compiling)"
fi

# ---- 4. no git repository at all ------------------------------------------
N="$T/norepo"
mkdir "$N" && git -C "$ROOT" archive HEAD | tar -x -C "$N" || fail "setup: git archive export"
cp "$ROOT/build.cmd" "$N/build.cmd"
run_build "$N"
[ "$RC" -ne 0 ] || fail "4: tree without a repository built with rc=0 (silent fallback)"
compiled "$N" && fail "4: no repository, yet compiled (not refused before compiling)"

# ---- 5. git top level is not the build.cmd folder -------------------------
P="$T/parent"
mkdir -p "$P/sub" && git -C "$ROOT" archive HEAD | tar -x -C "$P/sub" || fail "setup: export into subfolder"
cp "$ROOT/build.cmd" "$P/sub/build.cmd"
git -C "$P" init -q &&
  git -C "$P" add -A >/dev/null 2>&1 &&
  git -c user.name=t -c user.email=t@t -C "$P" commit -q -m one >/dev/null 2>&1 || fail "setup: parent repo"
run_build "$P/sub"
[ "$RC" -ne 0 ] || fail "5: build.cmd in a subfolder of a foreign repo built with rc=0"
compiled "$P/sub" && fail "5: foreign top level, yet compiled (not refused before compiling)"

# ---- 6. replace refs / grafts ---------------------------------------------
if full_clone replaced; then
  R="$T/replaced"
  if git -C "$R" rev-parse -q --verify HEAD~2 >/dev/null; then
    git -C "$R" replace HEAD~1 HEAD~2 >/dev/null 2>&1 || fail "setup: git replace"
    run_build "$R"
    [ "$RC" -ne 0 ] || fail "6: replace ref present, built with rc=0"
    compiled "$R" && fail "6: replace ref present, yet compiled (not refused before compiling)"
  else
    fail "setup: history too short for a replace ref"
  fi
fi
if full_clone grafted; then
  R="$T/grafted"
  mkdir -p "$R/.git/info"
  echo "$(git -C "$R" rev-parse HEAD)" > "$R/.git/info/grafts"   # HEAD made parentless
  run_build "$R"
  [ "$RC" -ne 0 ] || fail "6: .git/info/grafts present, built with rc=0"
  compiled "$R" && fail "6: grafts present, yet compiled (not refused before compiling)"
fi

# ---- 6b. repository without commits: rev-list --count gives no number -------
E="$T/empty"
mkdir "$E" && git -C "$E" init -q || fail "setup: empty repo"
git -C "$ROOT" archive HEAD | tar -x -C "$E" || fail "setup: export into empty repo"
cp "$ROOT/build.cmd" "$E/build.cmd"
run_build "$E"
[ "$RC" -ne 0 ] || fail "6b: repo with no commits built with rc=0 (fallback number)"
compiled "$E" && fail "6b: repo with no commits, yet compiled (not refused before compiling)"

# ---- 7. installer version equals AssemblyInfo.cs --------------------------
# build-msi.cmd cannot run here (needs nuget/WiX, network), so this is a static
# check of the default it sets: a literal default must match AssemblyInfo.cs,
# otherwise the script must read AssemblyInfo.cs itself.
ASM=$(grep -a -o 'AssemblyVersion("[0-9.]*")' "$ROOT/AssemblyInfo.cs" | sed 's/.*("\(.*\)")/\1/')
ASM3=$(echo "$ASM" | cut -d. -f1-3)
MSI="$ROOT/installer/build-msi.cmd"
# Real run, in a sandbox that has the script and AssemblyInfo.cs but no
# DIIISPL.exe: the script passes the version/channel checks, then stops at the
# "no DIIISPL.exe" guard, before any download. One line is added to the COPY,
# right after the checks, to print the version they computed.
M="$T/msi"
mkdir -p "$M/installer"
cp "$ROOT/AssemblyInfo.cs" "$M/AssemblyInfo.cs"
sed 's/^\(endlocal & set .*\)\r\?$/\1\r\necho COMPUTED=%VERSION%\r/' "$MSI" > "$M/installer/build-msi.cmd"
grep -aq '^echo COMPUTED=' "$M/installer/build-msi.cmd" || fail "7: setup: probe line not inserted"
msi_sandbox() {   # env assignments... -> OUT, RC
  OUT=$(cd "$M" && env "$@" cmd //c ".\\installer\\build-msi.cmd" 2>&1 </dev/null)
  RC=$?
}
if [ -z "$ASM3" ]; then
  fail "7: cannot read AssemblyVersion from AssemblyInfo.cs"
else
  msi_sandbox DIIISPL_CHANNEL=stable
  GOTV=$(echo "$OUT" | tr -d '\r' | sed -n 's/^COMPUTED=//p')
  [ "$GOTV" = "$ASM3" ] || fail "7: build-msi.cmd computed version '$GOTV', AssemblyInfo.cs says $ASM3"
  [ "$RC" -ne 0 ] || fail "7: sandbox run reached the end (rc=0): no stop before download?"

  # 7b. a normal value passes the env checks (and still stops before any download)
  msi_sandbox DIIISPL_VERSION=0.2.4 DIIISPL_CHANNEL=stable
  echo "$OUT" | grep -aqi 'refused' && fail "7b: normal DIIISPL_VERSION/CHANNEL refused"
  [ "$(echo "$OUT" | tr -d '\r' | sed -n 's/^COMPUTED=//p')" = "0.2.4" ] || fail "7b: DIIISPL_VERSION=0.2.4 not handed on"
  msi_sandbox DIIISPL_VERSION=0.2.4-dev.1 DIIISPL_CHANNEL=DEV
  echo "$OUT" | grep -aqi 'refused' || fail "7b: version with a suffix should be refused by build-msi.cmd (digits and dots only)"
fi

# ---- 8. build-msi.cmd refuses hostile env values before running them ------
# Refusal comes before any download (nuget/WiX); a value that gets through would
# print PWNED. Values: command separator, and a double quote.
msi_env() {
  OUT=$(cd "$ROOT" && env "$1" cmd //c ".\\installer\\build-msi.cmd" 2>&1 </dev/null)
  RC=$?
}
for assign in 'DIIISPL_VERSION=1 & echo PWNED' 'DIIISPL_CHANNEL=dev & echo PWNED' 'DIIISPL_VERSION=1"x' 'DIIISPL_CHANNEL=dev"x'; do
  msi_env "$assign"
  [ "$RC" -ne 0 ] || fail "8: build-msi.cmd accepted $assign (rc=0)"
  echo "$OUT" | grep -aq 'PWNED' && fail "8: build-msi.cmd ran the command inside $assign"
  echo "$OUT" | grep -aqi 'refused' || fail "8: build-msi.cmd did not refuse $assign"
done

# ---------------------------------------------------------------------------
if [ "$FAILS" -ne 0 ]; then
  exit 1
fi
echo OK
