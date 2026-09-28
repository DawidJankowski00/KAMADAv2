#!/bin/sh
# Downloads the OpenStreetMap extracts of the configured voivodeships from Geofabrik and merges them into one
# file for the self-hosted OSRM container. Runs once per region list – the volume keeps the result, and changing
# OSRM_REGIONS downloads only the missing extracts and merges again.
set -eu

if [ -f /data/region.osm.pbf ] && [ "$(cat /data/region.regions 2>/dev/null)" = "${OSRM_REGIONS}" ]; then
    echo "OSM extract for '${OSRM_REGIONS}' already prepared."
    exit 0
fi

apt-get update -qq
apt-get install -y -qq --no-install-recommends ca-certificates curl osmium-tool > /dev/null

mkdir -p /data/extracts
set --
for region in ${OSRM_REGIONS}; do
    file="/data/extracts/${region}.osm.pbf"
    if [ ! -f "${file}" ]; then
        echo "Downloading ${region} ..."
        curl -fsSL --retry 3 -o "${file}.part" "${OSRM_GEOFABRIK_URL%/}/${region}-latest.osm.pbf"
        mv "${file}.part" "${file}"
    fi
    set -- "$@" "${file}"
done

echo "Merging $# extract(s) ..."
rm -f /data/region.osm.pbf /data/region.regions
if [ $# -eq 1 ]; then
    cp "$1" /data/region.osm.pbf
else
    osmium merge --no-progress -o /data/region.osm.pbf "$@"
fi
echo "${OSRM_REGIONS}" > /data/region.regions
