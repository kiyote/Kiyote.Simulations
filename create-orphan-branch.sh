set -euo pipefail


# validate
[ -z "$1" ] && echo "ERROR: branch-name is a required input" && exit 1
BRANCH_NAME="$1"


# set up a git user
git config user.name "github-actions[bot]"
git config user.email "actions@users.noreply.github.com"


# make sure we've got all branches from the remote
# fetched locally before we call `git show-branch`
# (actions/checkout does not fetch all branches)
git fetch


# check if the branch already exists
if git show-branch "remotes/origin/$BRANCH_NAME"; then
    echo "INFO: branch $BRANCH_NAME already exists, exiting..."
    exit 0
fi


# if it doesn't exist, create one
echo "INFO: branch $BRANCH_NAME does not exist, creating a new orphan branch..."

# create the orphan branch and push it
git checkout --orphan "$BRANCH_NAME"
git rm -rf .
rm -f '.gitignore'
echo "# $BRANCH_NAME" > README.md
git add README.md
git commit -m 'init'
git push origin "$BRANCH_NAME"

# cleanup
git checkout --force -