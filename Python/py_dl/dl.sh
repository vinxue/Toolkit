# Read User Name and Password
echo -n Username:
read user

echo -n Password:
read -s password
echo

# Set folder name of link0 and link1
OLD_IFS="$IFS"
IFS="/"
array_link0=($1)
array_link1=($2)
IFS="$OLD_IFS"

for var in ${array_link0[@]}
do
    export name_link0=$var
done

for var in ${array_link1[@]}
do
    export name_link1=$var
done

if [ "$3" != "link1" ]; then
mkdir $name_link0
cd $name_link0
wget --user=$user --password=$password $1/link0.img.gz

gzip -d *.gz
cd ..
fi

if [ "$3" != "link0" ]; then
mkdir $name_link1
cd $name_link1
wget --user=$user --password=$password $2/link1.img.gz

gzip -d *.gz
cd ..
fi
