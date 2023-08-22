#!/usr/bin/env bash

#
# Copyright (c) 2022-2023 Carbon Community
# All rights reserved.
#

HOME=$(pwd)
TEMP="$(dirname "$0")/../../../Carbon.Core/.tmp"

if [ ! -d "$TEMP" ]; then
    mkdir -p "$TEMP"
fi

echo "** Git Metadata:"

cd "$TEMP"
git branch --show-current > .gitbranch
echo "**   Branch done."

git rev-parse --short HEAD > .gitchs
echo "**   Hash-Long done."

git rev-parse --long HEAD > .gitchl
echo "**   Hash-Long done."

git show -s --format="%an" HEAD > .gitauthor
echo "**   Author done."

git log -1 --pretty="%B" HEAD > .gitcomment
echo "**   Comment done."

git log -1 --format="%ci" HEAD > .gitdate
echo "**   Date done."

git remote get-url origin > .giturl
echo "**   URL done."

git log -1 --name-status --format="" > .gitchanges
echo "**   Changes done."

cd "$HOME"